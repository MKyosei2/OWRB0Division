using UnityEngine;

namespace OJikaProto
{
    public enum FlowState { Title, Playing, Completed }

    public class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }

        [Header("Text")]
        public string gameTitle = "OJI-KA";
        public string subtitle = "CASE 01 : 終電のいない駅";
        [TextArea] public string conceptLine = "“規約”が戦闘ルールを変える / 調査が交渉を変える";

        [TextArea]
        public string nextHookLine =
            "次回フック：\n" +
            "『駅の改札は、誰の記憶を吸っている？』\n" +
            "“期限付き停戦”の猶予が切れる前に、次の現場へ。";

        [Header("References (auto-find if empty)")]
        public EpisodeController episode;
        public PlayerController player;
        public PlayerCombat playerCombat;
        public LockOnController lockOn;
        public ThirdPersonCameraRig cameraRig;

        public FlowState State { get; private set; } = FlowState.Title;
        public NegotiationOutcome LastOutcome { get; private set; } = NegotiationOutcome.None;

        private float _baseFixedDelta;

        private void Update()
        {
            // Failsafe: if the recap UI (or any other transient UI) left the game paused,
            // restore time so player controls work. Camera scripts often still respond to input
            // when Time.timeScale == 0, which can look like "only the camera works".
            if (State == FlowState.Playing)
            {
                var recapVisible = (ProtoRecapUI.Instance != null && ProtoRecapUI.Instance.IsVisible);
                if (!recapVisible && Time.timeScale <= 0f)
                {
                    ProtoDiagnostics.Warn("flow.failsafe", "Flow failsafe: timeScale<=0 while Playing and recap not visible. Forcing time normal.", this, 1.0f);
                    ForceTimeNormal();
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            CoreEnsure.EnsureAll();
            _baseFixedDelta = Time.fixedDeltaTime;

            if (episode == null) episode = FindObjectOfType<EpisodeController>();
            if (player == null) player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                if (playerCombat == null) playerCombat = player.GetComponent<PlayerCombat>();
                if (lockOn == null) lockOn = player.GetComponent<LockOnController>();
            }
            if (cameraRig == null) cameraRig = FindObjectOfType<ThirdPersonCameraRig>();

            if (EventBus.Instance != null)
                EventBus.Instance.OnEpisodeComplete += OnEpisodeComplete;

            SetState(FlowState.Title);
        }

        private void OnDestroy()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnEpisodeComplete -= OnEpisodeComplete;
        }

        private void OnEpisodeComplete(NegotiationOutcome outcome)
        {
            LastOutcome = outcome;
            SetState(FlowState.Completed);
        }

        public void StartGame()
        {
            LastOutcome = NegotiationOutcome.None;
            SetState(FlowState.Playing);
            RunLogManager.Instance?.StartRun();
            // If an interrupted save exists, resume from it and show a 1-minute recap card.
            var save = ProtoSaveSystem.Load();
            if (episode != null && save != null && save.wasInterrupted)
            {
                episode.BeginEpisodeFromSave(save);
                ProtoRecapUI.Instance?.Show(save);
            }
            else
            {
                if (episode != null) episode.BeginEpisode();
            }
        }

        public void RestartEpisode()
        {
            LastOutcome = NegotiationOutcome.None;
            // Fresh start clears resume state
            ProtoSaveSystem.Clear();
            SetState(FlowState.Playing);
            RunLogManager.Instance?.StartRun();
            if (episode != null) episode.BeginEpisode();
        }

        public void BackToTitle()
        {
            LastOutcome = NegotiationOutcome.None;
            SetState(FlowState.Title);
        }

        private void ForceTimeNormal()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }

        private void SetState(FlowState s)
        {
            ProtoDiagnostics.Warn("flow.failsafe", "Flow failsafe: timeScale<=0 while Playing and recap not visible. Forcing time normal.", this, 1.0f);
                    ForceTimeNormal();

            State = s;
            bool canControl = (State == FlowState.Playing);

            if (player != null) player.enabled = canControl;
            if (playerCombat != null) playerCombat.enabled = canControl;
            if (lockOn != null) lockOn.enabled = canControl;

            if (cameraRig != null) cameraRig.enabled = true;

            if (episode != null) episode.autoStart = false;

            if (State == FlowState.Playing)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            EventBus.Instance?.Toast($"Flow: {State}");
        }
    }
}
