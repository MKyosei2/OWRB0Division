using UnityEngine;

namespace OJikaProto
{
    public enum EpisodePhaseType { Intro, Investigation, Combat, Outro }

    [System.Serializable]
    public class EpisodePhase
    {
        public EpisodePhaseType phaseType = EpisodePhaseType.Intro;
        public string title = "Phase";
        [TextArea] public string description = "";
        public int targetEvidenceCount = 2;
        public GameObject enemyPrefab;
        public NegotiationDefinition negotiationDef;
    }

    [System.Serializable]
    public class OutroTextSet
    {
        public NegotiationOutcome outcome = NegotiationOutcome.Truce;
        [TextArea] public string line1;
        [TextArea] public string line2;
        [TextArea] public string line3;
    }

    [CreateAssetMenu(menuName = "OJikaProto/EpisodeDefinition", fileName = "EpisodeDefinition_Case01")]
    public class EpisodeDefinition : ScriptableObject
    {
        public string episodeName = "終電のいない駅（プロト）";

        public EpisodePhase[] phases = new EpisodePhase[]
        {
            new EpisodePhase{ phaseType=EpisodePhaseType.Intro, title="導入", description="Enterで開始" },
            new EpisodePhase{ phaseType=EpisodePhaseType.Investigation, title="調査", description="調査ポイントに近づいてEで証拠を取得。証拠が揃ったらEnter。", targetEvidenceCount=2 },
            new EpisodePhase{ phaseType=EpisodePhaseType.Combat, title="収束作戦", description="規約→崩し→交渉（F）で決着へ。" },
            new EpisodePhase{ phaseType=EpisodePhaseType.Outro, title="後日談", description="Enterで完了" },
        };

        public OutroTextSet[] outroTexts = new OutroTextSet[]
        {
            new OutroTextSet{ outcome=NegotiationOutcome.Truce,   line1="駅員は『何も起きていない』と言い張った。", line2="あなたはメモに“期限”とだけ書き残す。", line3="次の終電が来るまで、猶予は短い。" },
            new OutroTextSet{ outcome=NegotiationOutcome.Contract, line1="怪異は条件付きで協力を受け入れた。", line2="代償は“見ないこと”。あなたは頷く。", line3="駅の影が、味方になった気がした。" },
            new OutroTextSet{ outcome=NegotiationOutcome.Seal,    line1="封印は完了した。空気が軽くなる。", line2="ただし“封”は永久ではないと直感する。", line3="あなたは次の場所へ向かう準備を始めた。" },
            new OutroTextSet{ outcome=NegotiationOutcome.Slay,    line1="討伐。静寂だけが残った。", line2="監視映像は、肝心な瞬間だけ欠けていた。", line3="胸に、割り切れない違和感が残る。" },
        };
    }

    public class EpisodeController : MonoBehaviour
    {
        public EpisodeDefinition episode;
        public CombatDirector combatDirector;

        public bool autoStart = false;

        public int PhaseIndex { get; private set; }
        public EpisodePhase Current => (episode != null && PhaseIndex < episode.phases.Length) ? episode.phases[PhaseIndex] : null;

        // --- Checkpoint / Resume support (for 20min break + 1-week return) ---
        public string CurrentCheckpointId { get; private set; } = "EP1_START";
        public bool WasInterrupted { get; private set; } = false;

        public NegotiationOutcome LastOutcome { get; private set; } = NegotiationOutcome.None;
        public bool IsComplete { get; private set; }

        private PlayerCombat _playerCombat;
        private LockOnController _lockOn;

        private void Awake()
        {
            CoreEnsure.EnsureAll();
            _playerCombat = FindObjectOfType<PlayerCombat>();
            _lockOn = FindObjectOfType<LockOnController>();

            if (combatDirector == null)
            {
                combatDirector = FindObjectOfType<CombatDirector>();
                if (combatDirector == null) combatDirector = new GameObject("CombatDirector").AddComponent<CombatDirector>();
            }
        }

        private void Start()
        {
            if (episode == null)
            {
                Debug.LogError("EpisodeDefinition が未設定です。");
                enabled = false;
                return;
            }
            if (autoStart) BeginEpisode();
        }

        public void BeginEpisode()
        {
            IsComplete = false;
            LastOutcome = NegotiationOutcome.None;
            PhaseIndex = 0; 

            CurrentCheckpointId = "EP1_START";
            WasInterrupted = false;

                        // ✅ 規約の伏せ字/解析状態を初期化（調査で特定する前提）
            RuleManager.Instance?.ResetDiscovery();
            RuleManager.Instance?.ClearRuntime();
            EnterPhase();
        }

        /// <summary>
        /// Continue an episode from a saved checkpoint.
        /// </summary>
        public void BeginEpisodeFromSave(ProtoSaveState state)
        {
            if (state == null || string.IsNullOrEmpty(state.checkpointId))
            {
                BeginEpisode();
                return;
            }

            IsComplete = false;
            LastOutcome = NegotiationOutcome.None;
            WasInterrupted = state.wasInterrupted;
            CurrentCheckpointId = state.checkpointId;

            // Map checkpoint to a phase.
            PhaseIndex = FindPhaseIndexForCheckpoint(state.checkpointId);

            // Ensure rule discovery state is coherent
            RuleManager.Instance?.ResetDiscovery();
            RuleManager.Instance?.ClearRuntime();

            EnterPhase();
        }

        public void NextPhase()
        {
                        Debug.Log($"OJK:DIAG ep.next fromIdx={PhaseIndex} cp={CurrentCheckpointId}");
PhaseIndex++;
            if (episode == null || PhaseIndex >= episode.phases.Length)
            {
                IsComplete = true;
                EventBus.Instance?.Toast("Episode Complete");
                EventBus.Instance?.EpisodeCompleted(LastOutcome);
                return;
            }
            EnterPhase();
        }

        // ✅ デバッグ：戦闘へ即ジャンプ（撮影/検証用）
        public void DebugJumpToCombat()
        {
            if (episode == null || episode.phases == null) return;
            for (int i = 0; i < episode.phases.Length; i++)
            {
                if (episode.phases[i].phaseType == EpisodePhaseType.Combat)
                {
                    PhaseIndex = i;
                    EnterPhase();
                    EventBus.Instance?.Toast("Debug: Jump to COMBAT");
                    return;
                }
            }
        }

        private int FindPhaseIndexForCheckpoint(string checkpointId)
        {
            if (episode == null || episode.phases == null) return 0;

            // Default mappings for EP1
            if (checkpointId == "EP1_BREAK")
            {
                // Start of Combat
                for (int i = 0; i < episode.phases.Length; i++)
                    if (episode.phases[i].phaseType == EpisodePhaseType.Combat) return i;
            }
            if (checkpointId == "EP1_END")
            {
                // Start of Outro
                for (int i = 0; i < episode.phases.Length; i++)
                    if (episode.phases[i].phaseType == EpisodePhaseType.Outro) return i;
            }
            if (checkpointId == "EP1_INVEST")
            {
                for (int i = 0; i < episode.phases.Length; i++)
                    if (episode.phases[i].phaseType == EpisodePhaseType.Investigation) return i;
            }

            return 0;
        }

        private void SetCheckpoint(string checkpointId, string nextObjective)
        {
            CurrentCheckpointId = checkpointId;

            // Save minimal state (AI-less recap uses fixed templates)
            var state = new ProtoSaveState
            {
                caseId = "EP1",
                checkpointId = checkpointId,
                wasInterrupted = true,
                nextObjective = nextObjective ?? "",
                // For prototype: tags/cards are provided by ProtoRecapDatabase templates (AI-less).
                ruleTags = new string[0],
                evidenceCardId = ""
            };
            ProtoSaveSystem.Save(state);
            WasInterrupted = true;
        }

        private void EnterPhase()
        {
var p = Current;
                        Debug.Log($"OJK:DIAG ep.enter idx={PhaseIndex} type={p?.phaseType} cp={CurrentCheckpointId}");
if (p == null) return;

            // Map internal episode phases to player-facing phases.
            var phaseDirector = ProtoPhaseDirector.Instance;

            // Movement is allowed in Investigation/Combat only.
            bool combatEnabled = (p.phaseType == EpisodePhaseType.Combat);
            if (_playerCombat) _playerCombat.enabled = combatEnabled;
            if (_lockOn) _lockOn.enabled = combatEnabled;

            EventBus.Instance?.Toast($"Phase: {p.title}");

            switch (p.phaseType)
            {
                case EpisodePhaseType.Intro:
                    phaseDirector?.SetPhase(ProtoPhase.Story, p.description, "STORY START", "事件概要と目的を確認する");
                    break;

                case EpisodePhaseType.Investigation:
                    phaseDirector?.SetPhase(ProtoPhase.Investigation, "証拠を集め、規約を特定する", "INVESTIGATION START", "証拠を1〜2個回収して次へ");
                    InvestigationManager.Instance.ResetForEpisode(p.targetEvidenceCount);
                    // 調査開始時に、規約は一旦「？？？」に戻して解析をやり直す
                    RuleManager.Instance?.ResetDiscovery();
                    // Checkpoint: early safe resume point
                    SetCheckpoint("EP1_INVEST", "証拠を集め、異界突入の準備を整える");
                    break;

                case EpisodePhaseType.Combat:
                    phaseDirector?.SetPhase(ProtoPhase.Combat, "規約を守りつつブレイクし、交渉へ持ち込む", "COMBAT START", "規約→崩し→交渉");
                    // Checkpoint: recommended break point (~20min)
                    SetCheckpoint("EP1_BREAK", "異界で規約を突破し、ブレイク→交渉へ持ち込む");
                    combatDirector.BeginCombat(p.enemyPrefab, p.negotiationDef, this);
                    break;

                case EpisodePhaseType.Outro:
                    phaseDirector?.SetPhase(ProtoPhase.Result, "結果を確認し、次のフックを見る", "RESULT", "第1話の結末");
                    // Episode end checkpoint (optional)
                    SetCheckpoint("EP1_END", "第1話完了。次の現場へ");
                    break;
            }
        }



        public void OnCombatResolved(NegotiationOutcome outcome)
        {
            LastOutcome = outcome;
            CaseMetaManager.Instance?.ApplyOutcome(outcome);
            EventBus.Instance?.Toast($"Resolved: {outcome}");
            // After a successful resolution, this is no longer an "interrupted" run.
            var s = ProtoSaveSystem.Load();
            if (s != null)
            {
                s.lastOutcome = outcome;
                s.wasInterrupted = false;
                ProtoSaveSystem.Save(s);
            }
            NextPhase();
        }

        public bool TryGetOutroText(NegotiationOutcome outcome, out string l1, out string l2, out string l3)
        {
            l1 = l2 = l3 = "";
            if (episode == null || episode.outroTexts == null) return false;

            for (int i = 0; i < episode.outroTexts.Length; i++)
            {
                var t = episode.outroTexts[i];
                if (t != null && t.outcome == outcome)
                {
                    l1 = t.line1; l2 = t.line2; l3 = t.line3;
                    return true;
                }
            }
            return false;
        }
    }
}
