// Assets/Scripts/Proto_Rules.cs
using System.Collections.Generic;
using UnityEngine;

namespace OJikaProto
{
    public enum RuleType { None, GazeProhibition, RepeatAttackProhibition, HazardFloorProhibition }

    [CreateAssetMenu(menuName = "OJikaProto/RuleDefinition", fileName = "RuleDefinition_Gaze")]
    public class RuleDefinition : ScriptableObject
    {
        public RuleType ruleType = RuleType.GazeProhibition;

        public string displayName = "視線を合わせるな";
        [TextArea] public string description = "ロックオンを維持し過ぎると違反。";

        public float gazeSecondsToViolate = 3.0f;
        public int repeatCountToViolate = 3;
        public float repeatWindowSeconds = 2.0f;

        public bool enrageEnemy = true;
        public bool toast = true;

        [Range(0f, 1f)] public float feedbackIntensity = 0.85f; // ✅ 演出の強さ
    }

    internal class RuleState
    {
        public float gazeT;
        public int repeatCount;
        public float repeatWindowT;

        public void Reset()
        {
            gazeT = 0f;
            repeatCount = 0;
            repeatWindowT = 0f;
        }
    }

    public class RuleManager : SimpleSingleton<RuleManager>
    {
        public List<RuleDefinition> activeRules = new();

        private PlayerCombat _playerCombat;
        private LockOnController _lockOn;
        private EnemyController _enemy;

        private readonly Dictionary<RuleDefinition, RuleState> _states = new();

        private void Start()
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null)
            {
                _playerCombat = pc.GetComponent<PlayerCombat>();
                _lockOn = pc.GetComponent<LockOnController>();
            }
        }

        private void Update()
        {
            if (_playerCombat == null || !_playerCombat.enabled) return;
            if (_enemy == null) _enemy = FindObjectOfType<EnemyController>();

            foreach (var rule in activeRules)
            {
                if (rule == null) continue;

                if (!_states.TryGetValue(rule, out var st))
                {
                    st = new RuleState();
                    _states.Add(rule, st);
                }

                switch (rule.ruleType)
                {
                    case RuleType.GazeProhibition: EvalGaze(rule, st); break;
                    case RuleType.RepeatAttackProhibition: EvalRepeat(rule, st); break;
                    case RuleType.HazardFloorProhibition: break; // 未使用
                }
            }
        }

        public void ClearRuntime()
        {
            foreach (var kv in _states) kv.Value.Reset();
        }

        private void Violate(RuleDefinition rule, string reason)
        {
            RunLogManager.Instance?.LogViolation(rule.displayName, reason);

            if (rule.toast)
                EventBus.Instance?.Toast($"Violation: {rule.displayName}");

            // ✅ 演出トリガー（フラッシュ/SE/台詞）
            EventBus.Instance?.RuleViolated(rule.displayName, reason, rule.feedbackIntensity);

            if (rule.enrageEnemy && _enemy)
                _enemy.Enrage();
        }

        private void EvalGaze(RuleDefinition rule, RuleState st)
        {
            bool locked = _lockOn && _lockOn.enabled && _lockOn.IsLockedOn;
            if (!locked) { st.gazeT = 0f; return; }

            st.gazeT += Time.deltaTime;
            if (st.gazeT >= rule.gazeSecondsToViolate)
            {
                st.gazeT = 0f;
                Violate(rule, $"LockOn>{rule.gazeSecondsToViolate:0.0}s");
            }
        }

        private void EvalRepeat(RuleDefinition rule, RuleState st)
        {
            if (st.repeatWindowT > 0f)
            {
                st.repeatWindowT -= Time.deltaTime;
                if (st.repeatWindowT <= 0f) { st.repeatCount = 0; st.repeatWindowT = 0f; }
            }

            if (Time.time - _playerCombat.LastAttackTime < 0.05f)
            {
                if (st.repeatWindowT <= 0f)
                {
                    st.repeatWindowT = rule.repeatWindowSeconds;
                    st.repeatCount = 1;
                }
                else st.repeatCount++;

                if (st.repeatCount >= rule.repeatCountToViolate)
                {
                    st.repeatCount = 0;
                    st.repeatWindowT = 0f;
                    Violate(rule, $"Repeat x{rule.repeatCountToViolate}");
                }
            }
        }
    }
}
