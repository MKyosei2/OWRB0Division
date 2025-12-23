using System.Text;
using UnityEngine;

namespace OJikaProto
{
    [System.Serializable]
    public class NegotiationOption
    {
        public string label = "停戦（期限付き）";
        [TextArea] public string description = "期限付きの停戦を提案する。";
        [Range(0f, 1f)] public float baseChance = 0.65f;

        public EvidenceTag[] evidenceBonusTags; // 揃っていれば +10%/個（最大+20%）
        public NegotiationOutcome success = NegotiationOutcome.Truce;
    }

    [CreateAssetMenu(menuName = "OJikaProto/NegotiationDefinition", fileName = "NegotiationDefinition_Case01")]
    public class NegotiationDefinition : ScriptableObject
    {
        public string title = "停戦交渉";
        [TextArea] public string prompt = "怪異は崩れている。今なら条件次第で収束できる。";

        public NegotiationOption[] options = new NegotiationOption[3]
        {
            new NegotiationOption{ label="停戦（期限付き）", baseChance=0.65f, success=NegotiationOutcome.Truce },
            new NegotiationOption{ label="契約（協力）", baseChance=0.50f, success=NegotiationOutcome.Contract },
            new NegotiationOption{ label="封印（儀式）", baseChance=0.45f, success=NegotiationOutcome.Seal },
        };

        public float cooldownSeconds = 12f;
        public bool failEnrage = true;
    }

    public class NegotiationManager : SimpleSingleton<NegotiationManager>
    {
        public const float BonusPerEvidence = 0.10f;
        public const float MaxBonus = 0.20f;

        public bool IsOpen { get; private set; }
        public NegotiationDefinition Current { get; private set; }
        public float Cooldown { get; private set; }

        private EpisodeController _episode;
        private EnemyController _enemy;
        private CombatDirector _director;

        private void Update()
        {
            if (Cooldown > 0f) Cooldown -= Time.deltaTime;
        }

        public void ResetCooldown() => Cooldown = 0f;

        public void Begin(NegotiationDefinition def, EpisodeController episode, EnemyController enemy, CombatDirector director)
        {
            if (def == null) { EventBus.Instance?.Toast("No NegotiationDef"); return; }
            if (Cooldown > 0f) { EventBus.Instance?.Toast("Negotiation Cooldown"); return; }

            IsOpen = true;
            Current = def;
            _episode = episode;
            _enemy = enemy;
            _director = director;

            FeedbackManager.Instance?.OnNegotiationOpen();
            EventBus.Instance?.Toast("Negotiation Open");
        }

        public void Close()
        {
            IsOpen = false;
            Current = null;
            _episode = null;
            _enemy = null;
            _director = null;
        }

        public bool TryComputeChance(
            int optionIndex,
            out float baseChance,
            out float bonus,
            out float penalty,
            out float finalChance,
            out int have,
            out int total)
        {
            baseChance = 0f; bonus = 0f; penalty = 0f; finalChance = 0f; have = 0; total = 0;
            if (!IsOpen || Current == null) return false;
            if (optionIndex < 0 || optionIndex >= Current.options.Length) return false;

            var opt = Current.options[optionIndex];
            baseChance = Mathf.Clamp01(opt.baseChance);

            total = (opt.evidenceBonusTags != null) ? opt.evidenceBonusTags.Length : 0;

            have = 0;
            if (opt.evidenceBonusTags != null && InvestigationManager.Instance != null)
            {
                for (int i = 0; i < opt.evidenceBonusTags.Length; i++)
                    if (InvestigationManager.Instance.Has(opt.evidenceBonusTags[i])) have++;
            }

            bonus = Mathf.Min(MaxBonus, have * BonusPerEvidence);

            penalty = (RunLogManager.Instance != null) ? RunLogManager.Instance.GetNegotiationPenalty() : 0f;
            finalChance = Mathf.Clamp01(baseChance + bonus - penalty);
            return true;
        }

        public static string EvidenceListToText(EvidenceTag[] tags)
        {
            if (tags == null || tags.Length == 0) return "（有利証拠なし）";
            var sb = new StringBuilder();
            for (int i = 0; i < tags.Length; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(tags[i]);
            }
            return sb.ToString();
        }

        public void Choose(int idx)
        {
            if (!IsOpen || Current == null) return;
            if (idx < 0 || idx >= Current.options.Length) return;

            float baseC, bonus, penalty, chance;
            int have, total;
            TryComputeChance(idx, out baseC, out bonus, out penalty, out chance, out have, out total);

            var opt = Current.options[idx];
            bool success = Random.value <= chance;

            RunLogManager.Instance?.LogNegotiation(opt.label, chance, success);

            if (success)
            {
                FeedbackManager.Instance?.OnNegotiationSuccess();
                EventBus.Instance?.Toast($"Negotiation Success: {opt.success}");
                _director?.ResolveByNegotiation(opt.success);
                Close();
            }
            else
            {
                FeedbackManager.Instance?.OnNegotiationFail();
                EventBus.Instance?.Toast($"Negotiation Failed ({chance:P0})");
                Cooldown = Current.cooldownSeconds;
                if (Current.failEnrage && _enemy) _enemy.Enrage();
                Close();
            }
        }
    }
}
