using UnityEngine;

namespace OJikaProto
{
    public enum RuleType
    {
        GazeProhibition,
        RepeatAttackProhibition
    }

    [CreateAssetMenu(menuName = "OJikaProto/RuleDefinition", fileName = "RuleDefinition_New")]
    public class RuleDefinition : ScriptableObject
    {
        public RuleType ruleType = RuleType.GazeProhibition;
        public string displayName = "Rule";
        [Range(0f, 1f)] public float feedbackIntensity = 0.85f;

        [Header("GazeProhibition")]
        public float gazeSecondsToViolate = 3.0f;

        [Header("RepeatAttackProhibition")]
        public int repeatCountToViolate = 3;
        public float repeatWindowSeconds = 2.0f;
    }

    public class RuleManager : SimpleSingleton<RuleManager>
    {
        public System.Collections.Generic.List<RuleDefinition> activeRules = new();

        private PlayerCombat _playerCombat;
        private LockOnController _lockOn;

        // gaze runtime
        private float _gazeT;

        // repeat runtime
        private AttackType _lastType = AttackType.None;
        private int _repeatCount;
        private float _repeatWindowT;

        // ✅ UI用：現在の進捗を公開（読み取り専用）
        public float GazeTimerSeconds => _gazeT;
        public float RepeatWindowSeconds => _repeatWindowT;
        public int RepeatCount => _repeatCount;
        public AttackType RepeatAttackType => _lastType;

        private void Start()
        {
            _playerCombat = FindObjectOfType<PlayerCombat>();
            _lockOn = FindObjectOfType<LockOnController>();
        }

        public void ClearRuntime()
        {
            _gazeT = 0f;
            _lastType = AttackType.None;
            _repeatCount = 0;
            _repeatWindowT = 0f;
        }

        private void Update()
        {
            if (activeRules == null || activeRules.Count == 0) return;

            if (_playerCombat == null) _playerCombat = FindObjectOfType<PlayerCombat>();
            if (_lockOn == null) _lockOn = FindObjectOfType<LockOnController>();

            foreach (var r in activeRules)
            {
                if (!r) continue;

                switch (r.ruleType)
                {
                    case RuleType.GazeProhibition:
                        TickGaze(r);
                        break;
                    case RuleType.RepeatAttackProhibition:
                        TickRepeat(r);
                        break;
                }
            }
        }

        private void TickGaze(RuleDefinition r)
        {
            bool gazing = (_lockOn != null && _lockOn.IsLockedOn && _lockOn.Target != null);
            if (gazing) _gazeT += Time.deltaTime;
            else _gazeT = Mathf.Max(0f, _gazeT - Time.deltaTime * 2f);

            if (_gazeT >= r.gazeSecondsToViolate)
            {
                _gazeT = 0f;
                Violate(r, "視線を合わせ続けた");
            }
        }

        private void TickRepeat(RuleDefinition r)
        {
            if (_repeatWindowT > 0f) _repeatWindowT -= Time.deltaTime;
            else { _repeatCount = 0; _lastType = AttackType.None; }

            if (_playerCombat == null) return;

            AttackType t = _playerCombat.LastAttackType;
            float at = _playerCombat.LastAttackTime;

            // 直近0.2秒以内の攻撃だけ見る（誤検出抑制）
            if (Time.time - at > 0.2f) return;
            if (t == AttackType.None) return;

            if (_repeatWindowT <= 0f)
            {
                _repeatWindowT = r.repeatWindowSeconds;
                _repeatCount = 1;
                _lastType = t;
                return;
            }

            if (_lastType == t) _repeatCount++;
            else { _lastType = t; _repeatCount = 1; }

            if (_repeatCount >= r.repeatCountToViolate)
            {
                _repeatCount = 0;
                _repeatWindowT = 0f;
                Violate(r, "同じ手を続けた");
            }
        }

        private void Violate(RuleDefinition r, string reason)
        {
            RunLogManager.Instance?.LogViolation(r.displayName, reason);
            EventBus.Instance?.RuleViolated(r.displayName, reason, r.feedbackIntensity);
        }
    }
}
