// Assets/Scripts/Proto_Flow.cs
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

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            CoreEnsure.EnsureAll();

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
            if (episode != null) episode.BeginEpisode();
        }

        public void RestartEpisode()
        {
            LastOutcome = NegotiationOutcome.None;
            SetState(FlowState.Playing);
            if (episode != null) episode.BeginEpisode();
        }

        public void BackToTitle()
        {
            LastOutcome = NegotiationOutcome.None;
            SetState(FlowState.Title);
        }

        private void SetState(FlowState s)
        {
            State = s;

            // 操作制御（タイトル/完了中は動けない）
            bool canControl = (State == FlowState.Playing);

            if (player != null) player.enabled = canControl;
            if (playerCombat != null) playerCombat.enabled = canControl;
            if (lockOn != null) lockOn.enabled = canControl;

            // カメラは常に動いて良い（見栄え優先）
            if (cameraRig != null) cameraRig.enabled = true;

            // エピソード自動進行はFlowが行う
            if (episode != null) episode.autoStart = false;

            // マウス制御（簡易）
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
