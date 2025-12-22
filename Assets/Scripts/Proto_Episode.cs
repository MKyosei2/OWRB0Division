// Assets/Scripts/Proto_Episode.cs
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
            new EpisodePhase{ phaseType=EpisodePhaseType.Combat, title="収束作戦", description="規約→崩し→交渉（F）で決着へ。", targetEvidenceCount=0 },
            new EpisodePhase{ phaseType=EpisodePhaseType.Outro, title="後日談", description="Enterで終了（プロト）" },
        };

        [Header("Outro Text (3 lines per outcome)")]
        public OutroTextSet[] outroTexts = new OutroTextSet[]
        {
            new OutroTextSet{
                outcome = NegotiationOutcome.Truce,
                line1 = "駅員は『何も起きていない』と言い張った。",
                line2 = "あなたはメモに“期限”とだけ書き残す。",
                line3 = "次の終電が来るまで、猶予は短い。"
            },
            new OutroTextSet{
                outcome = NegotiationOutcome.Contract,
                line1 = "怪異は条件付きで協力を受け入れた。",
                line2 = "代償は“見ないこと”。あなたは頷く。",
                line3 = "駅の影が、味方になった気がした。"
            },
            new OutroTextSet{
                outcome = NegotiationOutcome.Seal,
                line1 = "封印は完了した。空気が軽くなる。",
                line2 = "ただし“封”は永久ではないと直感する。",
                line3 = "あなたは次の場所へ向かう準備を始めた。"
            },
            new OutroTextSet{
                outcome = NegotiationOutcome.Slay,
                line1 = "討伐。静寂だけが残った。",
                line2 = "駅の監視映像は、肝心な瞬間だけ欠けていた。",
                line3 = "あなたの胸に、割り切れない違和感が残る。"
            },
        };
    }

    public class EpisodeController : MonoBehaviour
    {
        public EpisodeDefinition episode;
        public CombatDirector combatDirector;

        [Header("Flow")]
        public bool autoStart = true; // ✅ Flow導入後は false 推奨

        public int PhaseIndex { get; private set; }
        public EpisodePhase Current => (episode != null && PhaseIndex < episode.phases.Length) ? episode.phases[PhaseIndex] : null;

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

            if (autoStart)
                BeginEpisode();
        }

        public void BeginEpisode()
        {
            IsComplete = false;
            LastOutcome = NegotiationOutcome.None;

            PhaseIndex = 0;
            EnterPhase();
        }

        public void NextPhase()
        {
            PhaseIndex++;
            if (episode == null || PhaseIndex >= episode.phases.Length)
            {
                IsComplete = true;
                EventBus.Instance?.Toast("Episode Complete");
                EventBus.Instance?.EpisodeCompleted(LastOutcome); // ✅ Flowへ通知
                return;
            }
            EnterPhase();
        }

        private void EnterPhase()
        {
            var p = Current;
            if (p == null) return;

            bool combatEnabled = (p.phaseType == EpisodePhaseType.Combat);
            if (_playerCombat) _playerCombat.enabled = combatEnabled;
            if (_lockOn) _lockOn.enabled = combatEnabled;

            EventBus.Instance?.Toast($"Phase: {p.title}");

            switch (p.phaseType)
            {
                case EpisodePhaseType.Intro:
                    break;

                case EpisodePhaseType.Investigation:
                    InvestigationManager.Instance.ResetForEpisode(p.targetEvidenceCount);
                    break;

                case EpisodePhaseType.Combat:
                    combatDirector.BeginCombat(p.enemyPrefab, p.negotiationDef, this);
                    break;

                case EpisodePhaseType.Outro:
                    break;
            }
        }

        public void OnCombatResolved(NegotiationOutcome outcome)
        {
            LastOutcome = outcome;
            EventBus.Instance?.Toast($"Resolved: {outcome}");
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
