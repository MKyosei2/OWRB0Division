using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// 撮影用：プレイヤーを自動操作してデモを完全放置で成立させる。
    /// - 移動：敵へ接近/間合い維持
    /// - 攻撃：Light/Heavy/Sealをローテ（同じ手連打を避ける）
    /// - BREAK中：交渉を自動で開始→1番を選択（停戦）
    /// </summary>
    public class Proto_AutoPilot : MonoBehaviour
    {
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

        private int _attackStep = 0;
        private float _nextNegotiateTime = 0f;

        private void Start()
        {
            _active = enabledByDefault;
            RefreshRefs();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _active = !_active;
                SubtitleManager.Instance?.Add($"【AUTO PILOT】{(_active ? "ON" : "OFF")}", 1.4f);

                if (!_active) ReleaseControl();
            }

            if (!_active) return;

            RefreshRefs();
            Tick();
        }

        public void BeginForDemo()
        {
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
            // タイトル/完了中は触らない（Flowが無い場合でも安全）
            var flow = FindObjectOfType<GameFlowController>();
            if (flow != null && flow.State != FlowState.Playing)
            {
                ReleaseControl();
                return;
            }

            if (_pc == null) return;

            // 戦闘が存在するなら戦闘のオート
            bool inCombat = (_episode != null && _episode.Current != null && _episode.Current.phaseType == EpisodePhaseType.Combat);
            if (inCombat)
            {
                CombatAutopilot();
            }
            else
            {
                // 調査/導入などは「ゆっくり前進」だけ（絵を動かす用）
                IdleWander();
            }
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
                // 敵参照が切れている（生成し直し等）→再取得
                _enemy = FindObjectOfType<EnemyController>();
                _enemyBreak = _enemy ? _enemy.GetComponent<Breakable>() : null;
                _director = FindObjectOfType<CombatDirector>();
            }

            if (_enemy == null)
            {
                // 敵がいないなら立ち止まる
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

            // 目線（ロックオンは使わず、視線規約のトリガを増やさない）
            if (toEnemy.sqrMagnitude > 0.001f)
                _pc.externalLookDirWorld = toEnemy.normalized;

            // 間合い維持
            Vector3 targetPos;
            if (dist > desiredDistance)
            {
                targetPos = e - toEnemy.normalized * desiredDistance;
            }
            else
            {
                // 近すぎるときは少し下がる
                targetPos = p - toEnemy.normalized * 0.35f;
            }

            Vector3 move = targetPos - p;
            move.y = 0f;
            if (move.sqrMagnitude > 0.02f)
                _pc.externalMoveWorld = move.normalized;
            else
                _pc.externalMoveWorld = Vector3.zero;

            // BREAKになったら交渉を自動開始→選択
            if (_enemyBreak != null && _enemyBreak.IsBroken)
            {
                if (_breakSeenT < 0f) _breakSeenT = Time.unscaledTime;

                if (Time.unscaledTime >= _nextNegotiateTime)
                {
                    AutoNegotiation();
                }

                return;
            }

            // 一定時間経ってもBREAKしないならデモとして強制BREAK（リテイク削減）
            if (_enemyBreak != null && !_enemyBreak.IsBroken && _combatT >= forceBreakAfterSeconds)
            {
                _enemyBreak.ApplyBreakDamage(99999f);
                SubtitleManager.Instance?.Add("※デモ補正：BREAKを確定（撮影安定化）", 2.0f);
                return;
            }

            // 攻撃：間合いに入っているならローテ
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

            // Light → Heavy → Seal の循環（同じ手連打を避ける）
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

            // 既に開いているなら少し待って選ぶ
            if (nm.IsOpen)
            {
                if (Time.unscaledTime - _breakSeenT >= negotiationOpenDelay)
                {
                    nm.Choose(autoChooseIndex);
                    _nextNegotiateTime = Time.unscaledTime + 2.0f;
                }
                return;
            }

            if (_episode == null || _episode.Current == null || _episode.Current.negotiationDef == null) return;
            if (_director == null) _director = FindObjectOfType<CombatDirector>();
            if (_director == null || _enemy == null) return;

            // クールダウン等はnm側で弾く
            nm.Begin(_episode.Current.negotiationDef, _episode, _enemy, _director);
            _breakSeenT = Time.unscaledTime;
            _nextNegotiateTime = Time.unscaledTime + 0.4f;
        }

        private void ReleaseControl()
        {
            if (_pc != null)
            {
                _pc.externalControl = false;
                _pc.externalMoveWorld = Vector3.zero;
                _pc.externalLookDirWorld = Vector3.zero;
            }
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
    }
}
