using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// 撮影用：プレイヤーを自動操作してデモを完全放置で成立させる。
    /// 注意：通常プレイでは無効推奨。ProtoBuildConfig.AllowCaptureTools && (シーン許可) が前提。
    /// </summary>
    public class Proto_AutoPilot : MonoBehaviour
    {
        [Header("Guard")]
        [Tooltip("指定したシーン名の時だけ有効。空なら全シーン許可（非推奨）。")]
        public string[] allowedSceneNames = new[] { "Demo", "Prototype", "Title" };

        [Header("Toggle")]
        public KeyCode toggleKey = KeyCode.F6;
        public bool enabledByDefault = false;

        [Header("Combat")]
        public float desiredDistance = 1.45f;
        public float engageDistance = 1.75f;
        public float attackInterval = 0.85f;
        public float forceBreakAfterSeconds = 10f;

        [Header("Negotiation")]
        public int autoChooseIndex = 0; // 0=停戦
        public float negotiationOpenDelay = 0.6f;

        private bool _active;
        private float _combatT;
        private float _attackT;
        private float _breakSeenT = -999f;

        private PlayerController _pc;
        private PlayerCombat _combat;
        private EnemyController _enemy;
        private Breakable _enemyBreak;
        private CombatDirector _director;
        private EpisodeController _episode;

        private int _attackStep;
        private float _nextNegotiateTime;

        private bool AllowedNow()
        {
            if (!ProtoBuildConfig.AllowCaptureTools) return false;
            if (!ProtoBuildConfig.IsSceneAllowed(allowedSceneNames)) return false;
            return true;
        }

        private void Awake()
        {
            if (!AllowedNow())
            {
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            ProtoDiagnostics.RequireAnyPlayer(this);
            _active = enabledByDefault;
            RefreshRefs();
        }

        private void Update()
        {
            if (!AllowedNow()) return;

            if (Input.GetKeyDown(toggleKey))
            {
                _active = !_active;
                SubtitleManager.Instance?.Add($"AUTO PILOT : {(_active ? "ON" : "OFF")}", 1.2f);

                if (!_active) ReleaseControl();
            }

            if (!_active) return;

            RefreshRefs();
            Tick();
        }

        public void BeginForDemo()
        {
            if (!AllowedNow()) return;
            _active = true;
            _combatT = 0f;
            _attackT = 0f;
            _breakSeenT = -999f;
            _attackStep = 0;
            _nextNegotiateTime = 0f;
        }

        public void EndForDemo()
        {
            _active = false;
            ReleaseControl();
        }

        private void Tick()
        {
            // タイトル/完了中は触らない
            var flow = FindObjectOfType<GameFlowController>();
            if (flow != null && flow.State != FlowState.Playing)
            {
                ReleaseControl();
                return;
            }

            if (_pc == null) return;

            bool inCombat = (_episode != null && _episode.Current != null && _episode.Current.phaseType == EpisodePhaseType.Combat);
            if (inCombat) CombatAutopilot();
            else IdleWander();
        }

        private void IdleWander()
        {
            _pc.externalControl = true;

            Vector3 fwd = _pc.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;

            _pc.externalMoveWorld = fwd.normalized * 0.35f;
            _pc.externalLookDirWorld = fwd.normalized;
        }

        private void CombatAutopilot()
        {
            _pc.externalControl = true;

            if (_enemy == null || _enemyBreak == null || (_enemy != null && _enemy.GetComponent<Damageable>()?.IsDead == true))
            {
                _enemy = FindObjectOfType<EnemyController>();
                _enemyBreak = _enemy ? _enemy.GetComponent<Breakable>() : null;
                _director = FindObjectOfType<CombatDirector>();
            }

            if (_enemy == null)
            {
                _pc.externalMoveWorld = Vector3.zero;
                _pc.externalLookDirWorld = _pc.transform.forward;
                return;
            }

            _combatT += Time.unscaledDeltaTime;

            Vector3 p = _pc.transform.position;
            Vector3 e = _enemy.transform.position;

            Vector3 toEnemy = e - p;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;

            if (toEnemy.sqrMagnitude > 0.001f)
                _pc.externalLookDirWorld = toEnemy.normalized;

            Vector3 targetPos;
            if (dist > desiredDistance)
                targetPos = e - toEnemy.normalized * desiredDistance;
            else
                targetPos = p - toEnemy.normalized * 0.35f;

            Vector3 move = targetPos - p;
            move.y = 0f;
            _pc.externalMoveWorld = (move.sqrMagnitude > 0.02f) ? move.normalized : Vector3.zero;

            // BREAK → 交渉
            if (_enemyBreak != null && _enemyBreak.IsBroken)
            {
                if (_breakSeenT < 0f) _breakSeenT = Time.unscaledTime;

                if (Time.unscaledTime >= _nextNegotiateTime)
                    AutoNegotiation();

                return;
            }

            // デモ安定化：一定時間で強制BREAK
            if (_enemyBreak != null && !_enemyBreak.IsBroken && _combatT >= forceBreakAfterSeconds)
            {
                _enemyBreak.ApplyBreakDamage(99999f);
                SubtitleManager.Instance?.Add("※デモ補正：BREAKを確定（撮影安定化）", 2.0f);
                ProtoDiagnostics.TrackCounter("demo.force_break", 1);
                return;
            }

            // 攻撃ローテ
            _attackT += Time.unscaledDeltaTime;
            if (dist <= engageDistance && _attackT >= attackInterval)
            {
                _attackT = 0f;
                DoAttackRotation();
            }
        }

        private void DoAttackRotation()
        {
            if (_combat == null) return;

            AttackType type = (_attackStep % 3) switch
            {
                0 => AttackType.Light,
                1 => AttackType.Heavy,
                _ => AttackType.Seal
            };

            _attackStep++;
            _combat.PerformAttack(type);
        }

        private void AutoNegotiation()
        {
            var nm = NegotiationManager.Instance;
            if (nm == null) return;

            if (nm.IsOpen)
            {
                if (Time.unscaledTime - _breakSeenT >= negotiationOpenDelay)
                {
                    if (nm.HasCounterOffer) nm.AcceptCounterOffer();
                    else nm.Choose(autoChooseIndex);

                    _nextNegotiateTime = Time.unscaledTime + 2.0f;
                }
                return;
            }

            if (_episode == null || _episode.Current == null || _episode.Current.negotiationDef == null) return;
            if (_director == null) _director = FindObjectOfType<CombatDirector>();
            if (_director == null || _enemy == null) return;

            nm.Begin(_episode.Current.negotiationDef, _episode, _enemy, _director);
            _breakSeenT = Time.unscaledTime;
            _nextNegotiateTime = Time.unscaledTime + 0.4f;
        }

        private void ReleaseControl()
        {
            if (_pc == null) return;
            _pc.externalControl = false;
            _pc.externalMoveWorld = Vector3.zero;
            _pc.externalLookDirWorld = Vector3.zero;
        }

        private void RefreshRefs()
        {
            if (_pc == null) _pc = FindObjectOfType<PlayerController>();
            if (_combat == null) _combat = FindObjectOfType<PlayerCombat>();
            if (_episode == null) _episode = FindObjectOfType<EpisodeController>();
            if (_enemy == null) _enemy = FindObjectOfType<EnemyController>();
            if (_enemyBreak == null && _enemy != null) _enemyBreak = _enemy.GetComponent<Breakable>();
            if (_director == null) _director = FindObjectOfType<CombatDirector>();
        }

        private void OnDisable()
        {
            ReleaseControl();
        }
    }
}
