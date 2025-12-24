﻿using System.Text;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Step3: 交渉を「確率UI中心」から一段降ろし、
    /// ・証拠タグの充足（ゲート）
    /// ・最後の取引（譲歩＝行政コスト）／対案（カウンターオファー）
    /// で成立させる。
    /// 
    /// 目的：運ゲーに見せず、調査と判断で前進が起きるプロトにする。
    /// </summary>

    public enum NegotiationStance
    {
        Firm = 0,      // 譲歩なし（条件が厳しい）
        Balanced = 1,  // 小さな譲歩（条件を1つ緩和）
        Concede = 2    // 大きな譲歩（条件を2つ緩和）
    }

    [System.Serializable]
    public class NegotiationOption
    {
        public string label = "停戦（期限付き）";
        [TextArea] public string description = "期限付きの停戦を提案する。";

        // Legacy：旧プロト用（確率計算はStep3で使用しない）
        [Range(0f, 1f)] public float baseChance = 0.65f;

        // Step3：このタグ群を「成立条件」として扱う（揃うほど成立しやすい）
        public EvidenceTag[] evidenceBonusTags;

        // 0以上なら「必要数」を固定。-1なら evidenceBonusTags.Length を必要数にする
        public int minEvidenceToSucceed = -1;

        public NegotiationOutcome success = NegotiationOutcome.Truce;

        [Header("Step7: Special")]
        [Tooltip("TRUEなら『緊急停戦（パス消費）』など、メタ要素で追加される特殊オプション。")]
        public bool isEmergencyOption = false;

        [Tooltip("TRUEなら成功時に CaseMetaManager.arbitrationPasses を1消費する")]
        public bool consumesArbitrationPass = false;

        [Tooltip("成功時に追加で加算する行政コスト（0..1）")]
        [Range(0f, 1f)] public float extraAdminCost = 0.35f;

        [Tooltip("成功時に追加で加算する期限（truceDebt）")]
        [Range(0, 3)] public int extraTruceDebt = 1;
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

        [Header("Seal Ritual (Step4)")]
        [Tooltip("TRUEの場合、『封印（儀式）』は成立条件を満たした後に入力ミニゲームを要求する。")]
        public bool sealRitualEnabled = true;

        [Tooltip("儀式の入力数（矢印キー）")]
        [Min(3)] public int sealRitualSteps = 4;

        [Tooltip("1入力の猶予秒")]
        [Min(0.3f)] public float sealStepTimeSeconds = 1.1f;

        [Tooltip("儀式失敗時の追加行政コスト")]
        [Range(0f, 1f)] public float sealFailAdminCost = 0.06f;
    }

    public class NegotiationManager : SimpleSingleton<NegotiationManager>
    {
        // 旧定数は互換のため残す（UI表示やログで使う場合がある）
        public const float BonusPerEvidence = 0.10f;
        public const float MaxBonus = 0.20f;

        // Step3：譲歩（行政コスト）と条件緩和
        public const int BalancedReduction = 1;
        public const int ConcedeReduction = 2;

        public const float BalancedAdminCost = 0.10f;
        public const float ConcedeAdminCost = 0.25f;

        // 学習（Fail Forward）を「条件緩和」に変換：+8%（交渉失敗1回）= -1条件
        public const float InsightPerGateReduction = 0.08f;

        // Step8: 期限/歪みで『書類仕事』が増え、同じ譲歩でも行政コストが上がる
        // （成功率は上がらないが、代償が増える＝“面倒さ/圧”が見える）
        public const float BureaucracyMulPerTruceDebt = 0.25f;
        public const float BureaucracyMulPerDistortion = 0.15f;

        public bool IsOpen { get; private set; }
        public NegotiationDefinition Current { get; private set; }
        public float Cooldown { get; private set; }

        public NegotiationStance CurrentStance { get; private set; } = NegotiationStance.Balanced;

        // カウンターオファー（対案）
        public bool HasCounterOffer { get; private set; }
        public string CounterOfferText { get; private set; }
        private int _counterOptionIndex = -1;
        private NegotiationStance _counterStance = NegotiationStance.Balanced;

        private EpisodeController _episode;
        private EnemyController _enemy;
        private CombatDirector _director;

        // -------------------- Step4：Seal Ritual --------------------
        public bool IsSealRitualActive { get; private set; }
        public KeyCode[] SealRitualSequence { get; private set; }
        public int SealRitualIndex { get; private set; }
        public float SealRitualStepTimeLeft { get; private set; }
        public float SealRitualStepTimeTotal { get; private set; }

        private int _pendingOptionIndex = -1;
        private NegotiationStance _pendingStance = NegotiationStance.Balanced;
        private float _pendingAdminCostDelta = 0f;
        private float _pendingGate = 0f;

        private void NormalizeEmergencyOptionForMeta()
        {
            if (Current == null) return;
            if (Current.options == null) Current.options = new NegotiationOption[0];

            var meta = CaseMetaManager.Instance;
            bool hasPass = (meta != null && meta.arbitrationPasses > 0);

            // 1) パスが無ければ、緊急オプションは配列から外す（UIを混乱させない）
            bool hasEmergency = false;
            int normalCount = 0;
            for (int i = 0; i < Current.options.Length; i++)
            {
                var o = Current.options[i];
                if (o == null) continue;
                if (o.isEmergencyOption) hasEmergency = true;
                else normalCount++;
            }

            if (!hasPass && hasEmergency)
            {
                var trimmed = new NegotiationOption[normalCount];
                int w = 0;
                for (int i = 0; i < Current.options.Length; i++)
                {
                    var o = Current.options[i];
                    if (o == null) continue;
                    if (o.isEmergencyOption) continue;
                    trimmed[w++] = o;
                }
                Current.options = trimmed;
                return;
            }

            if (!hasPass) return;

            // 2) 既に入っているなら何もしない
            if (hasEmergency) return;

            // 3) 追加
            var em = new NegotiationOption
            {
                label = "緊急停戦（パス消費）",
                description = "暫定許可証で停戦を強制する（代償：行政コストと期限が増える）",
                baseChance = 1f,
                evidenceBonusTags = new EvidenceTag[0],
                minEvidenceToSucceed = 0,
                success = NegotiationOutcome.Truce,
                isEmergencyOption = true,
                consumesArbitrationPass = true,
                extraAdminCost = 0.35f,
                extraTruceDebt = 1,
            };

            var ext = new NegotiationOption[Current.options.Length + 1];
            for (int i = 0; i < Current.options.Length; i++) ext[i] = Current.options[i];
            ext[ext.Length - 1] = em;
            Current.options = ext;
        }

        private void Update()
        {
            if (Cooldown > 0f) Cooldown -= Time.deltaTime;

            if (IsSealRitualActive)
            {
                // 交渉中はタイムスケールが揺れる可能性があるので unscaled を使う
                SealRitualStepTimeLeft -= Time.unscaledDeltaTime;
                if (SealRitualStepTimeLeft <= 0f)
                    FailSealRitual("TIMEOUT");
            }
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

            CurrentStance = NegotiationStance.Balanced;
            ClearCounterOffer();
            ClearSealRitual();

            // Step7: 停戦の次周回メリット（暫定許可証）に応じて、緊急オプションを増減
            NormalizeEmergencyOptionForMeta();

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

            ClearCounterOffer();
            ClearSealRitual();
        }

        private void ClearCounterOffer()
        {
            HasCounterOffer = false;
            CounterOfferText = null;
            _counterOptionIndex = -1;
            _counterStance = NegotiationStance.Balanced;
        }

        private void ClearSealRitual()
        {
            IsSealRitualActive = false;
            SealRitualSequence = null;
            SealRitualIndex = 0;
            SealRitualStepTimeLeft = 0f;
            SealRitualStepTimeTotal = 0f;
            _pendingOptionIndex = -1;
            _pendingStance = NegotiationStance.Balanced;
            _pendingAdminCostDelta = 0f;
            _pendingGate = 0f;
        }

        private static KeyCode RandArrowKey()
        {
            int r = Random.Range(0, 4);
            return r switch
            {
                0 => KeyCode.UpArrow,
                1 => KeyCode.LeftArrow,
                2 => KeyCode.DownArrow,
                _ => KeyCode.RightArrow,
            };
        }

        private void BeginSealRitual(int optionIndex, NegotiationStance stance, float adminCostDelta, float gate)
        {
            if (Current == null) return;

            _pendingOptionIndex = optionIndex;
            _pendingStance = stance;
            _pendingAdminCostDelta = adminCostDelta;
            _pendingGate = gate;

            int steps = Mathf.Max(3, Current.sealRitualSteps);
            SealRitualSequence = new KeyCode[steps];
            for (int i = 0; i < steps; i++)
            {
                // 連続同一をなるべく避ける
                KeyCode k = RandArrowKey();
                if (i > 0 && k == SealRitualSequence[i - 1]) k = RandArrowKey();
                SealRitualSequence[i] = k;
            }

            SealRitualIndex = 0;
            SealRitualStepTimeTotal = Mathf.Max(0.3f, Current.sealStepTimeSeconds);
            SealRitualStepTimeLeft = SealRitualStepTimeTotal;
            IsSealRitualActive = true;

            FeedbackManager.Instance?.OnNegotiationOpen();
            EventBus.Instance?.Toast("Seal Ritual");
        }

        public void InputSealRitual(KeyCode key)
        {
            if (!IsSealRitualActive || SealRitualSequence == null) return;

            // 予期せぬキーは無視（EscなどはUI側で扱う）
            if (SealRitualIndex < 0 || SealRitualIndex >= SealRitualSequence.Length) return;

            if (key == SealRitualSequence[SealRitualIndex])
            {
                SealRitualIndex++;
                if (SealRitualIndex >= SealRitualSequence.Length)
                {
                    SucceedSealRitual();
                    return;
                }

                SealRitualStepTimeLeft = SealRitualStepTimeTotal;
                return;
            }

            FailSealRitual("WRONG");
        }

        public void AbortSealRitual() => FailSealRitual("ABORT");

        private void SucceedSealRitual()
        {
            if (Current == null) { ClearSealRitual(); return; }
            int idx = _pendingOptionIndex;
            if (idx < 0 || idx >= Current.options.Length) { ClearSealRitual(); return; }

            var opt = Current.options[idx];

            // ログ（儀式後に確定）
            RunLogManager.Instance?.LogNegotiation(opt.label, _pendingGate, true);
            RunLogManager.Instance?.AddAdministrativeCost(_pendingAdminCostDelta);

            FeedbackManager.Instance?.OnNegotiationSuccess();
            EventBus.Instance?.Toast($"Seal Complete  (Cost +{_pendingAdminCostDelta:P0})");

            // 決着
            var director = _director;
            ClearSealRitual();
            director?.ResolveByNegotiation(NegotiationOutcome.Seal);
            Close();
        }

        private void FailSealRitual(string reason)
        {
            if (!IsSealRitualActive) return;

            // pending情報を保持したまま、ログ＆FailForward処理を行う
            if (Current != null && _pendingOptionIndex >= 0 && _pendingOptionIndex < Current.options.Length)
            {
                var opt = Current.options[_pendingOptionIndex];
                RunLogManager.Instance?.LogNegotiation(opt.label, _pendingGate, false);

                // 失敗は「前進（学習/解析）」へ
                int ins = (reason == "ABORT") ? 1 : 2;
                RuleManager.Instance?.GainInsightFromFailure(ins);
            }

            float extra = (Current != null) ? Mathf.Max(0f, Current.sealFailAdminCost) : 0.05f;
            RunLogManager.Instance?.AddAdministrativeCost(extra);

            // 再挑戦しやすくする
            Cooldown = (Current != null) ? Current.cooldownSeconds * 0.40f : 4f;

            // 儀式失敗の“危険”は見せる（ただし即死級にはしない）
            if (reason != "ABORT" && _enemy != null)
                _enemy.Enrage();

            FeedbackManager.Instance?.OnNegotiationFail();
            EventBus.Instance?.Toast($"Seal Failed ({reason})");

            ClearSealRitual();
            Close();
        }

        public void CycleStance(int dir)
        {
            int v = (int)CurrentStance + dir;
            v = Mathf.Clamp(v, 0, 2);
            SetStance((NegotiationStance)v);
        }

        public void SetStance(NegotiationStance stance)
        {
            if (CurrentStance == stance) return;
            CurrentStance = stance;
            // プレイヤーが条件を変えたら、出していた対案は無効にする
            ClearCounterOffer();
        }

        public float GetBureaucracyCostMultiplier()
        {
            var meta = CaseMetaManager.Instance;
            if (meta == null) return 1f;
            return 1f
                   + BureaucracyMulPerTruceDebt * Mathf.Clamp(meta.truceDebt, 0, 3)
                   + BureaucracyMulPerDistortion * Mathf.Clamp(meta.distortion, 0, 3);
        }

        public float GetAdminCostDelta(NegotiationStance stance)
        {
            float baseCost = stance switch
            {
                NegotiationStance.Firm => 0f,
                NegotiationStance.Balanced => BalancedAdminCost,
                NegotiationStance.Concede => ConcedeAdminCost,
                _ => 0f
            };

            // Step8: 期限/歪みで同じ譲歩でも“書類コスト”が重くなる
            return Mathf.Clamp01(baseCost * GetBureaucracyCostMultiplier());
        }

        public int GetStanceReduction(NegotiationStance stance)
        {
            return stance switch
            {
                NegotiationStance.Firm => 0,
                NegotiationStance.Balanced => BalancedReduction,
                NegotiationStance.Concede => ConcedeReduction,
                _ => 0
            };
        }

        private int GetHaveEvidenceCount(NegotiationOption opt)
        {
            int have = 0;
            if (opt == null || opt.evidenceBonusTags == null || InvestigationManager.Instance == null) return 0;

            for (int i = 0; i < opt.evidenceBonusTags.Length; i++)
                if (InvestigationManager.Instance.Has(opt.evidenceBonusTags[i])) have++;

            return have;
        }

        private int GetRequiredEvidenceCount(NegotiationOption opt)
        {
            if (opt == null) return 0;
            if (opt.minEvidenceToSucceed >= 0) return opt.minEvidenceToSucceed;
            return (opt.evidenceBonusTags != null) ? opt.evidenceBonusTags.Length : 0;
        }

        private int GetInsightReduction()
        {
            float insight = (RunLogManager.Instance != null) ? RunLogManager.Instance.GetNegotiationInsightBonus() : 0f;
            if (insight <= 0f) return 0;
            return Mathf.FloorToInt(insight / Mathf.Max(0.0001f, InsightPerGateReduction));
        }

        /// <summary>
        /// Step3：確率ではなく「成立条件」を計算する。
        /// </summary>
        public bool TryComputeGate(
            int optionIndex,
            NegotiationStance stance,
            out int required,
            out int have,
            out int total,
            out int stanceReduce,
            out int insightReduce,
            out int finalRequired,
            out float adminCostDelta,
            out bool canSucceed)
        {
            required = have = total = stanceReduce = insightReduce = finalRequired = 0;
            adminCostDelta = 0f;
            canSucceed = false;

            if (!IsOpen || Current == null) return false;
            if (optionIndex < 0 || optionIndex >= Current.options.Length) return false;

            var opt = Current.options[optionIndex];
            total = (opt.evidenceBonusTags != null) ? opt.evidenceBonusTags.Length : 0;

            have = GetHaveEvidenceCount(opt);
            required = Mathf.Max(0, GetRequiredEvidenceCount(opt));

            stanceReduce = GetStanceReduction(stance);
            insightReduce = GetInsightReduction();

            finalRequired = Mathf.Max(0, required - stanceReduce - insightReduce);
            adminCostDelta = GetAdminCostDelta(stance);

            canSucceed = (have >= finalRequired);
            return true;
        }

        /// <summary>
        /// 互換用（旧UI/旧コードが呼んでもコンパイルが通るように残す）
        /// Step3では「成功率」は表示しない。finalChanceは“成立度”として返す。
        /// </summary>
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

            int required, stanceReduce, insightReduce, finalRequired;
            float adminCostDelta;
            bool canSucceed;
            TryComputeGate(optionIndex, CurrentStance, out required, out have, out total, out stanceReduce, out insightReduce, out finalRequired, out adminCostDelta, out canSucceed);

            // 0..1の「成立度」：have / max(1, finalRequired)
            finalChance = (finalRequired <= 0) ? 1f : Mathf.Clamp01(have / Mathf.Max(1f, finalRequired));
            return true;
        }

        public static string EvidenceListToText(EvidenceTag[] tags)
        {
            if (tags == null || tags.Length == 0) return "（条件なし）";
            var sb = new StringBuilder();
            for (int i = 0; i < tags.Length; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(tags[i]);
            }
            return sb.ToString();
        }

        public void AcceptCounterOffer()
        {
            if (IsSealRitualActive) return;
            if (!HasCounterOffer) return;
            // 対案の条件へ切り替え
            CurrentStance = _counterStance;
            HasCounterOffer = false;

            int idx = _counterOptionIndex;
            _counterOptionIndex = -1;
            CounterOfferText = null;

            // 受諾は成功する想定だが、念のため通常判定を通す
            Choose(idx);
        }

        public void Choose(int idx)
        {
            if (!IsOpen || Current == null) return;
            if (idx < 0 || idx >= Current.options.Length) return;

            if (IsSealRitualActive) return;

            ClearCounterOffer();

            // 成立条件チェック
            int required, have, total, stanceReduce, insightReduce, finalRequired;
            float adminCostDelta;
            bool canSucceed;
            TryComputeGate(idx, CurrentStance, out required, out have, out total, out stanceReduce, out insightReduce, out finalRequired, out adminCostDelta, out canSucceed);

            var opt = Current.options[idx];

            // 成立度（1.0=成立 / 0.0=不足）
            float gate = (finalRequired <= 0) ? 1f : Mathf.Clamp01(have / Mathf.Max(1f, finalRequired));

            if (canSucceed)
            {
                // Step7: 暫定許可証を使う『緊急停戦』
                // - 条件0で成立する代わりに、行政コストと期限が増える
                // - 成功時にパスを1消費する
                if (opt.isEmergencyOption)
                {
                    bool ok = true;
                    var meta = CaseMetaManager.Instance;
                    if (opt.consumesArbitrationPass)
                        ok = (meta != null && meta.ConsumeArbitrationPass());

                    if (!ok)
                    {
                        EventBus.Instance?.Toast("No Arbitration Pass");
                        FeedbackManager.Instance?.OnNegotiationFail();
                        Close();
                        return;
                    }

                    // Step8: 監査/歪みが強いほど緊急手続きのコストが跳ね上がる
                    float mul = GetBureaucracyCostMultiplier();
                    float extraCost = Mathf.Clamp01(opt.extraAdminCost * mul);
                    float totalCost = Mathf.Clamp01(adminCostDelta + extraCost);

                    RunLogManager.Instance?.LogNegotiation(opt.label, gate, true);
                    RunLogManager.Instance?.AddAdministrativeCost(totalCost);

                    int extraDebt = Mathf.Clamp(opt.extraTruceDebt, 0, 3);
                    if (extraDebt > 0 && meta != null) meta.AddTruceDebt(extraDebt);

                    FeedbackManager.Instance?.OnNegotiationSuccess();
                    EventBus.Instance?.Toast($"Emergency Truce  (Cost +{totalCost:P0})");

                    _director?.ResolveByNegotiation(opt.success);
                    Close();
                    return;
                }

                // Step4：封印だけは“成立後に儀式”を要求（運ゲー排除・手触り付与）
                if (opt.success == NegotiationOutcome.Seal && Current.sealRitualEnabled)
                {
                    BeginSealRitual(idx, CurrentStance, adminCostDelta, gate);
                    return;
                }

                RunLogManager.Instance?.LogNegotiation(opt.label, gate, true);
                // 譲歩コストの反映（成功率には影響しない）
                RunLogManager.Instance?.AddAdministrativeCost(adminCostDelta);

                FeedbackManager.Instance?.OnNegotiationSuccess();
                EventBus.Instance?.Toast($"Negotiation Success: {opt.success}  (Cost +{adminCostDelta:P0})");

                _director?.ResolveByNegotiation(opt.success);
                Close();
                return;
            }

            // 失敗ログ（封印儀式はここに来ない）
            RunLogManager.Instance?.LogNegotiation(opt.label, gate, false);

            // 失敗：対案を出す（最後の取引）
            // 1) 同じ選択肢で成立させるために必要な譲歩レベルを計算
            int requiredAfterInsight = Mathf.Max(0, required - insightReduce);
            int needReduction = Mathf.Max(0, requiredAfterInsight - have); // 0..∞

            if (needReduction <= ConcedeReduction)
            {
                // 譲歩レベルを提示
                _counterOptionIndex = idx;
                _counterStance = (needReduction <= BalancedReduction) ? NegotiationStance.Balanced : NegotiationStance.Concede;

                float c = GetAdminCostDelta(_counterStance);
                HasCounterOffer = true;
                CounterOfferText = $"対案：{opt.label} を成立させるには“譲歩”が必要（必要緩和 {needReduction}）\nEnter：譲歩を受諾（行政コスト +{c:P0}）  /  1-3：別案  /  Esc：閉じる";

                // Fail Forward：失敗は学習＋解析に変換
                RuleManager.Instance?.GainInsightFromFailure(1);
                EventBus.Instance?.Toast("Counter Offer");
                return;
            }

            // 2) 別の結末なら成立するか（例：停戦なら通る）
            int bestIdx = -1;
            NegotiationStance bestStance = NegotiationStance.Balanced;

            for (int i = 0; i < Current.options.Length; i++)
            {
                // まずはBalancedで判定し、無理ならConcedeで判定
                if (TrySucceedWithStance(i, NegotiationStance.Balanced, out _))
                {
                    bestIdx = i; bestStance = NegotiationStance.Balanced; break;
                }
                if (TrySucceedWithStance(i, NegotiationStance.Concede, out _))
                {
                    bestIdx = i; bestStance = NegotiationStance.Concede; break;
                }
            }

            if (bestIdx >= 0)
            {
                var best = Current.options[bestIdx];
                _counterOptionIndex = bestIdx;
                _counterStance = bestStance;

                float c = GetAdminCostDelta(bestStance);
                HasCounterOffer = true;
                CounterOfferText = $"対案：相手は『{best.label}』なら受諾可能\nEnter：対案を受諾（行政コスト +{c:P0}）  /  1-3：別案  /  Esc：閉じる";

                RuleManager.Instance?.GainInsightFromFailure(1);
                EventBus.Instance?.Toast("Counter Offer");
                return;
            }

            // 3) どうやっても今は無理：証拠不足として撤退（Fail Forwardで前進だけ残す）
            RuleManager.Instance?.GainInsightFromFailure(2);
            EventBus.Instance?.Toast("Insufficient Evidence");

            // 再挑戦のため、クールダウンは短めにする
            Cooldown = Current.cooldownSeconds * 0.35f;

            // 罰としての即エンレイジは弱める（2回目以降に発動）
            int fails = (RunLogManager.Instance != null) ? RunLogManager.Instance.NegotiationFailCount : 0;
            if (Current.failEnrage && _enemy && fails >= 2) _enemy.Enrage();

            Close();
        }

        private bool TrySucceedWithStance(int optionIndex, NegotiationStance stance, out int finalRequired)
        {
            int required, have, total, stanceReduce, insightReduce;
            float cost;
            bool can;
            TryComputeGate(optionIndex, stance, out required, out have, out total, out stanceReduce, out insightReduce, out finalRequired, out cost, out can);
            return can;
        }
    }
}
