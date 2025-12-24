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
        // -------------------- Fail Forward Tuning --------------------
        // 違反や交渉失敗は「成功率の単純デバフ」ではなく、
        // ・学習（次回の交渉成功率の上昇）
        // ・行政コスト（デブリーフ/評価での代償）
        // に変換する。プレイ体験として「ミスが前進に繋がる」ことを優先。

        // 学習ボーナス（成功率に加算）
        public const float InsightPerViolation = 0.03f;        // +3% / 規約違反
        public const float InsightPerNegotiationFail = 0.08f;  // +8% / 交渉失敗
        public const float InsightMax = 0.25f;                 // 最大 +25%

        // 行政コスト（表示/評価用。成功率には直接影響しない）
        public const float AdminCostPerViolation = 0.10f; // +10% / 違反
        public const float AdminCostPerHit = 0.03f;       // +3% / 被弾
        public const float AdminCostMax = 1.00f;          // 最大 100%

        // Break復帰：違反が増えるほど、Bureauの緊急措置で「Brokenが少し長く続く」
        // （プレイヤーに再交渉の窓を与える。代償は行政コスト）
        public const float BreakRecoverMulMin = 0.55f;          // 0.55 まで（長くなる）
        public const float BreakRecoverMulPerViolation = 0.10f; // -10% / 違反（1.0→0.9→0.8 ...）

        [Serializable] public class RuleViolation { public string ruleName; public string reason; public float time; }
        [Serializable] public class NegotiationLog { public string option; public float chance; public bool success; public float time; }

        public float RunStartTime { get; private set; }
        public int PlayerHitCount { get; private set; }
        public float PlayerDamageTaken { get; private set; }


        // 交渉などで発生する追加の行政コスト（譲歩・対案など）
        public float ExtraAdministrativeCost { get; private set; }
        public int ViolationCount { get; private set; }
        public int NegotiationFailCount { get; private set; }

        public readonly List<RuleViolation> Violations = new();
        public readonly List<NegotiationLog> Negotiations = new();

        public void StartRun()
        {
            RunStartTime = Time.time;
            PlayerHitCount = 0;
            PlayerDamageTaken = 0f;

            ViolationCount = 0;
            NegotiationFailCount = 0;


            ExtraAdministrativeCost = 0f;
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
            if (!success) NegotiationFailCount++;

            Negotiations.Add(new NegotiationLog
            {
                option = option,
                chance = chance,
                success = success,
                time = Time.time - RunStartTime
            });
        }

        // 互換用：旧「違反で成功率が下がる」ペナルティは廃止（Fail Forwardへ）
        public float GetNegotiationPenalty() => 0f;

        public float GetNegotiationInsightBonus()
        {
            float v = ViolationCount * InsightPerViolation + NegotiationFailCount * InsightPerNegotiationFail;
            return Mathf.Min(InsightMax, Mathf.Max(0f, v));
        }

        public void AddAdministrativeCost(float amount01)
        {
            ExtraAdministrativeCost += Mathf.Max(0f, amount01);
        }

        public float GetAdministrativeCost01()
        {
            float c = ViolationCount * AdminCostPerViolation + PlayerHitCount * AdminCostPerHit + ExtraAdministrativeCost;
            return Mathf.Min(AdminCostMax, Mathf.Max(0f, c));
        }

        public float GetBreakRecoverMultiplier()
        {
            float mul = 1f - (ViolationCount * BreakRecoverMulPerViolation);
            return Mathf.Max(BreakRecoverMulMin, mul);
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
            Ensure<InfiltrationManager>("InfiltrationManager");
            Ensure<CaseMetaManager>("CaseMetaManager");
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
