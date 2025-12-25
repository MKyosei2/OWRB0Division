using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJikaProto
{
    // =========================================================
    // Singleton base
    // =========================================================
    public abstract class SimpleSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this as T;
        }
    }

    // =========================================================
    // Shared Types (referenced across many scripts)
    // =========================================================

    /// <summary>
    /// 規約違反の通知（UI/ヒント用）。
    /// Proto_UI / Proto_ActionHintOverlay などが intensity を参照する。
    /// </summary>
    [Serializable]
    public struct RuleViolationSignal
    {
        public string ruleName;
        public string reason;
        public float intensity; // 0..1
    }

    /// <summary>
    /// 交渉の結果（複数スクリプトが参照する共通列挙）
    /// </summary>
    public enum NegotiationOutcome
    {
        None = 0,

        // Non-lethal resolutions
        Truce = 1,        // 停戦
        Contract = 2,     // 契約
        Seal = 3,         // 封印

        // Lethal resolution
        Slay = 4,         // 討伐

        // Negotiation failed
        Failed = 10
    }

    // =========================================================
    // EventBus (UI/Overlay/Flow hooks)
    // =========================================================
    /// <summary>
    /// 既存コードが EventBus を直接参照しているため、ここで復元。
    /// </summary>
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

    // =========================================================
    // RunLogManager (Fail Forward / Insight / AdminCost)
    // =========================================================
    /// <summary>
    /// 既存スクリプトが RunLogManager.Instance を参照しているため復元。
    /// ・難易度選択なし前提の詰み回避：連続死亡/違反/交渉失敗で "解析(Insight)" を強化
    /// ・ペナルティは進行停止ではなく、行政コスト（評価/演出）へ変換
    /// </summary>
    public class RunLogManager : SimpleSingleton<RunLogManager>
    {
        // -------------------- Fail Forward Tuning --------------------
        // 学習ボーナス（成功率やヒント強度に加算する想定）
        public const float InsightPerViolation = 0.03f;        // +3% / 規約違反
        public const float InsightPerNegotiationFail = 0.08f;  // +8% / 交渉失敗
        public const float InsightMax = 0.25f;                 // 最大 +25%

        // 行政コスト（表示/評価用。成功率には直接影響しない）
        public const float AdminCostPerViolation = 0.10f; // +10% / 違反
        public const float AdminCostPerHit = 0.03f;       // +3% / 被弾
        public const float AdminCostMax = 1.00f;          // 最大 100%

        // ブレイク復帰：違反が増えるほど「Brokenが少し長く続く」（交渉窓を増やす）
        public const float BreakRecoverMulMin = 0.55f;
        public const float BreakRecoverMulPerViolation = 0.10f;

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

        // ---- anti-stuck ----
        public int deaths { get; private set; }
        public int consecutiveDeaths { get; private set; }

        /// <summary>
        /// 0..1（裏での軽い救済係数）。連続死亡が強いシグナル。
        /// </summary>
        public float AssistFactor
        {
            get
            {
                float a = consecutiveDeaths * 0.18f + NegotiationFailCount * 0.06f;
                return Mathf.Clamp01(a);
            }
        }

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

            deaths = 0;
            consecutiveDeaths = 0;
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

        // ---- 互換API：Proto_Combat が呼ぶ ----
        public void LogPlayerDied()
        {
            deaths++;
            consecutiveDeaths++;
        }

        public void LogCombatResolved()
        {
            // 戦闘が解決（勝利/撤退/交渉決着など）したら連続死亡をリセット
            consecutiveDeaths = 0;
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

    // =========================================================
    // CoreEnsure (referenced in Proto_Episode/Proto_Flow/Proto_UI etc.)
    // =========================================================
    /// <summary>
    /// 既存コードが CoreEnsure を直接参照しているため復元。
    /// ※ Ensure対象の各 Manager 型は、プロジェクト内の既存クラス名に合わせてあります。
    ///    もしクラス名が違う場合は、ここだけ名前を合わせてください。
    /// </summary>
    public static class CoreEnsure
    {
        public static void EnsureAll()
        {
            Ensure<EventBus>("EventBus");
            Ensure<RunLogManager>("RunLogManager");

            // ここから先はプロジェクト側の既存 Manager に依存
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

    /// <summary>
    /// Scene に1つ置けば、起動時に必要なシングルトン群を保証する。
    /// OneShotSetup で生成される想定。
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            CoreEnsure.EnsureAll();
        }
    }
}
