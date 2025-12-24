using System.Collections.Generic;
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

        [Header("Discovery / Analysis")]
        [Tooltip("TRUEなら開始時は規約名が伏せられる（？？？表示）。")]
        public bool startHidden = true;

        [Tooltip("未特定時の表示ラベル")]
        public string hiddenLabel = "？？？";

        [Tooltip("解析が少し進んだ時だけ併記するヒント（任意）")]
        [TextArea] public string hintText = "";

        [Tooltip("このポイント数に到達すると『規約を特定』になる")]
        [Min(1)] public int confirmPointsRequired = 2;

        [Tooltip("この証拠を取ると解析ポイントが進む（取得済み判定はInvestigationManager.Has）")]
        public EvidenceTag[] clueEvidenceTags;

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

        // discovery runtime
        private readonly Dictionary<RuleDefinition, int> _analysisPoints = new();
        private readonly Dictionary<RuleDefinition, HashSet<EvidenceTag>> _appliedEvidence = new();

        // ✅ UI用：現在の進捗を公開（読み取り専用）
        public float GazeTimerSeconds => _gazeT;
        public float RepeatWindowSeconds => _repeatWindowT;
        public int RepeatCount => _repeatCount;
        public AttackType RepeatAttackType => _lastType;

        private void Start()
        {
            _playerCombat = FindObjectOfType<PlayerCombat>();
            _lockOn = FindObjectOfType<LockOnController>();

            // エピソード開始で呼ばれるのが理想だが、保険で初期化
            ResetDiscovery();
        }

        public void ClearRuntime()
        {
            _gazeT = 0f;
            _lastType = AttackType.None;
            _repeatCount = 0;
            _repeatWindowT = 0f;
        }

        /// <summary>
        /// 規約の伏せ字/解析状態をリセットする（調査フェーズ開始時に呼ぶ想定）
        /// </summary>
        public void ResetDiscovery()
        {
            _analysisPoints.Clear();
            _appliedEvidence.Clear();

            if (activeRules == null) return;
            for (int i = 0; i < activeRules.Count; i++)
            {
                var r = activeRules[i];
                if (!r) continue;

                int req = Mathf.Max(1, r.confirmPointsRequired);
                _analysisPoints[r] = r.startHidden ? 0 : req;
                _appliedEvidence[r] = new HashSet<EvidenceTag>();
            }
        }

        private void EnsureDiscoveryEntry(RuleDefinition r)
        {
            if (!r) return;

            if (!_analysisPoints.ContainsKey(r))
            {
                int req = Mathf.Max(1, r.confirmPointsRequired);
                _analysisPoints[r] = r.startHidden ? 0 : req;
            }
            if (!_appliedEvidence.ContainsKey(r))
                _appliedEvidence[r] = new HashSet<EvidenceTag>();
        }

        public int GetAnalysisPoints(RuleDefinition r)
        {
            if (!r) return 0;
            EnsureDiscoveryEntry(r);
            return _analysisPoints.TryGetValue(r, out int v) ? v : 0;
        }

        public int GetConfirmPointsRequired(RuleDefinition r)
        {
            if (!r) return 1;
            return Mathf.Max(1, r.confirmPointsRequired);
        }

        public bool IsRevealed(RuleDefinition r)
        {
            if (!r) return true;
            if (!r.startHidden) return true;

            EnsureDiscoveryEntry(r);
            int req = GetConfirmPointsRequired(r);
            return GetAnalysisPoints(r) >= req;
        }

        public string GetRulePanelLine(RuleDefinition r)
        {
            if (!r) return "";
            if (IsRevealed(r)) return r.displayName;

            int p = GetAnalysisPoints(r);
            int req = GetConfirmPointsRequired(r);

            string baseLabel = string.IsNullOrWhiteSpace(r.hiddenLabel) ? "？？？" : r.hiddenLabel;
            string hint = (p > 0 && !string.IsNullOrWhiteSpace(r.hintText)) ? $" {r.hintText}" : "";

            return $"{baseLabel}  解析 {p}/{req}{hint}";
        }

        public string GetPlayerFacingRuleName(RuleDefinition r)
        {
            if (!r) return "";
            return IsRevealed(r)
                ? r.displayName
                : (string.IsNullOrWhiteSpace(r.hiddenLabel) ? "？？？" : r.hiddenLabel);
        }

        public string GetPlayerFacingViolationReason(RuleDefinition r, string actualReason)
        {
            if (!r) return actualReason;
            if (IsRevealed(r)) return actualReason;

            int p = GetAnalysisPoints(r);
            int req = GetConfirmPointsRequired(r);
            return $"規約に抵触した（解析 {p}/{req}）";
        }

        
        /// <summary>
        /// Fail Forward：交渉失敗などで「学習」が発生した時、規約解析を少し進める。
        /// （特定済みの規約には影響しない）
        /// </summary>
        public void GainInsightFromFailure(int points = 1)
        {
            if (activeRules == null) return;
            int p = Mathf.Max(0, points);
            if (p <= 0) return;

            for (int i = 0; i < activeRules.Count; i++)
            {
                var r = activeRules[i];
                if (!r) continue;
                AddAnalysisPoint(r, p, toastOnGain: false);
            }
        }

public bool TryGetRuleByName(string displayName, out RuleDefinition rule)
        {
            rule = null;
            if (activeRules == null) return false;
            for (int i = 0; i < activeRules.Count; i++)
            {
                var r = activeRules[i];
                if (!r) continue;
                if (r.displayName == displayName) { rule = r; return true; }
            }
            return false;
        }

        private void AddAnalysisPoint(RuleDefinition r, int delta, bool toastOnGain)
        {
            if (!r) return;
            if (!r.startHidden) return;

            EnsureDiscoveryEntry(r);

            int req = GetConfirmPointsRequired(r);
            int prev = Mathf.Clamp(GetAnalysisPoints(r), 0, req);
            if (prev >= req) return;

            int add = Mathf.Max(0, delta);
            int next = Mathf.Clamp(prev + add, 0, req);
            if (next == prev) return;

            _analysisPoints[r] = next;

            if (toastOnGain)
                EventBus.Instance?.Toast($"規約解析 +{(next - prev)} ({next}/{req})");

            if (next >= req)
                EventBus.Instance?.Toast($"規約を特定：{r.displayName}");
        }

        private void TickDiscoveryFromEvidence(RuleDefinition r)
        {
            if (!r) return;
            if (!r.startHidden) return;
            if (IsRevealed(r)) return;
            if (r.clueEvidenceTags == null || r.clueEvidenceTags.Length == 0) return;

            var im = InvestigationManager.Instance;
            if (im == null) return;

            EnsureDiscoveryEntry(r);
            var applied = _appliedEvidence[r];

            for (int i = 0; i < r.clueEvidenceTags.Length; i++)
            {
                var tag = r.clueEvidenceTags[i];
                if (!im.Has(tag)) continue;
                if (applied.Contains(tag)) continue;

                applied.Add(tag);
                AddAnalysisPoint(r, 1, toastOnGain: true);

                if (IsRevealed(r))
                    break;
            }
        }

        private void Update()
        {
            if (activeRules == null || activeRules.Count == 0) return;

            if (_playerCombat == null) _playerCombat = FindObjectOfType<PlayerCombat>();
            if (_lockOn == null) _lockOn = FindObjectOfType<LockOnController>();

            foreach (var r in activeRules)
            {
                if (!r) continue;

                // 解析：証拠で特定する
                TickDiscoveryFromEvidence(r);

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
            // ランログは“真名”で保持（デバッグ/解析用）
            RunLogManager.Instance?.LogViolation(r.displayName, reason);

            // 違反でも解析は進む（Fail Forwardへ寄せる）
            AddAnalysisPoint(r, 1, toastOnGain: false);

            // プレイヤー表示は、特定前は伏せる
            string shownName = GetPlayerFacingRuleName(r);
            string shownReason = GetPlayerFacingViolationReason(r, reason);

            EventBus.Instance?.RuleViolated(shownName, shownReason, r.feedbackIntensity);
        }
    }
}
