using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJikaProto
{
    public abstract class SimpleSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this as T;
        }
    }

    [Serializable]
    public struct RuleViolationSignal
    {
        public string ruleName;
        public string reason;
        public float intensity; // 0-1
    }

    public enum NegotiationOutcome { None, Truce, Contract, Seal, Slay }

    public class EventBus : SimpleSingleton<EventBus>
    {
        public event Action<string> OnToast;
        public event Action OnPlayerDied;
        public event Action<RuleViolationSignal> OnRuleViolation;
        public event Action<NegotiationOutcome> OnEpisodeComplete;

        public void Toast(string msg) => OnToast?.Invoke(msg);
        public void PlayerDied() => OnPlayerDied?.Invoke();

        public void RuleViolated(string ruleName, string reason, float intensity = 1f)
        {
            OnRuleViolation?.Invoke(new RuleViolationSignal
            {
                ruleName = ruleName,
                reason = reason,
                intensity = Mathf.Clamp01(intensity)
            });
        }

        public void EpisodeCompleted(NegotiationOutcome outcome) => OnEpisodeComplete?.Invoke(outcome);
    }

    public class RunLogManager : SimpleSingleton<RunLogManager>
    {
        // 交渉成功率ペナルティ（違反ごとに下がる）
        public const float NegotiationPenaltyPerViolation = 0.05f; // -5%/回
        public const float NegotiationPenaltyMax = 0.30f;          // 最大-30%

        // Break復帰が早くなる（違反が多いほど崩し猶予が短い）
        public const float BreakRecoverBoostPerViolation = 0.20f; // +20%/回
        public const float BreakRecoverBoostMax = 1.00f;          // 最大+100%（x2.0）

        [Serializable] public class RuleViolation { public string ruleName; public string reason; public float time; }
        [Serializable] public class NegotiationLog { public string option; public float chance; public bool success; public float time; }

        public float RunStartTime { get; private set; }
        public int PlayerHitCount { get; private set; }
        public float PlayerDamageTaken { get; private set; }

        public int ViolationCount { get; private set; }
        public readonly List<RuleViolation> Violations = new();
        public readonly List<NegotiationLog> Negotiations = new();

        public void StartRun()
        {
            RunStartTime = Time.time;
            PlayerHitCount = 0;
            PlayerDamageTaken = 0f;
            ViolationCount = 0;
            Violations.Clear();
            Negotiations.Clear();
        }

        public void LogPlayerDamaged(float dmg)
        {
            PlayerHitCount++;
            PlayerDamageTaken += Mathf.Max(0f, dmg);
        }

        public void LogViolation(string ruleName, string reason)
        {
            ViolationCount++;
            Violations.Add(new RuleViolation
            {
                ruleName = ruleName,
                reason = reason,
                time = Time.time - RunStartTime
            });
        }

        public void LogNegotiation(string option, float chance, bool success)
        {
            Negotiations.Add(new NegotiationLog
            {
                option = option,
                chance = chance,
                success = success,
                time = Time.time - RunStartTime
            });
        }

        public float GetNegotiationPenalty() => Mathf.Min(NegotiationPenaltyMax, ViolationCount * NegotiationPenaltyPerViolation);

        public float GetBreakRecoverMultiplier()
        {
            float boost = Mathf.Min(BreakRecoverBoostMax, ViolationCount * BreakRecoverBoostPerViolation);
            return 1f + boost; // 1.0～2.0
        }
    }

    public static class CoreEnsure
    {
        public static void EnsureAll()
        {
            Ensure<EventBus>("EventBus");
            Ensure<RunLogManager>("RunLogManager");
            Ensure<InvestigationManager>("InvestigationManager");
            Ensure<RuleManager>("RuleManager");
            Ensure<NegotiationManager>("NegotiationManager");
        }

        private static void Ensure<T>(string name) where T : Component
        {
            if (UnityEngine.Object.FindObjectOfType<T>() != null) return;
            var go = new GameObject(name);
            go.AddComponent<T>();
        }
    }

    public class GameBootstrapper : MonoBehaviour
    {
        private void Awake() => CoreEnsure.EnsureAll();
    }
}
