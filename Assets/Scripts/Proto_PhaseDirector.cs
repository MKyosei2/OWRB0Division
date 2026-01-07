using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Central phase state machine to give "turn-based" clarity.
    /// Responsibilities:
    ///  - Hold current phase and a 1-line objective text.
    ///  - Enable/disable player subsystems per phase (movement / combat).
    ///  - Show a short phase transition card (paper theater "幕").
    ///  - Provide safe timeScale handling so we don't end up frozen.
    /// </summary>
    public class ProtoPhaseDirector : MonoBehaviour
    {
        public static ProtoPhaseDirector Instance { get; private set; }

        [Header("Auto-detect references")]
        public PlayerController player;
        public PlayerCombat playerCombat;
        public LockOnController lockOn;
        public ThirdPersonCameraRig cameraRig;

        [Header("Phase Card")]
        public bool showPhaseCard = true;
        public float phaseCardSeconds = 1.1f;

        public ProtoPhase CurrentPhase { get; private set; } = ProtoPhase.Story;
        public string CurrentObjective { get; private set; } = "";

        // card state
        private bool _cardVisible;
        private float _cardT;
        private string _cardTitle;
        private string _cardBody;

        private float _baseFixedDelta;

        // GUI resources
        private Texture2D _flat;
        private GUIStyle _title;
        private GUIStyle _body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindObjectOfType<ProtoPhaseDirector>() != null) return;
            var go = new GameObject("ProtoPhaseDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<ProtoPhaseDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _baseFixedDelta = Time.fixedDeltaTime;

            AutoFindRefs();
            EnsureGui();

            // Ensure Phase HUD exists
            if (FindObjectOfType<Proto_PhaseHUD>() == null)
            {
                var hudGo = new GameObject("Proto_PhaseHUD");
                DontDestroyOnLoad(hudGo);
                hudGo.AddComponent<Proto_PhaseHUD>();
            }

            // Default state
            SetPhase(ProtoPhase.Story, "");
        }

        private void AutoFindRefs()
        {
            if (player == null) player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                if (playerCombat == null) playerCombat = player.GetComponent<PlayerCombat>();
                if (lockOn == null) lockOn = player.GetComponent<LockOnController>();
            }
            if (cameraRig == null) cameraRig = FindObjectOfType<ThirdPersonCameraRig>();
        }

        private void EnsureGui()
        {
            if (_flat == null)
            {
                _flat = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _flat.SetPixel(0, 0, Color.white);
                _flat.Apply();
            }

            _title = new GUIStyle
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _title.normal.textColor = Color.white;

            _body = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _body.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
        }

        public void SetPhase(ProtoPhase phase, string objective, bool showCard = true)
        {
            CurrentPhase = phase;
            CurrentObjective = objective ?? "";

            // Always recover timeScale when changing phase.
            ForceTimeNormal();

            ApplyInputGatesForPhase(phase);

            if (this.showPhaseCard && showCard)
                ShowPhaseCard(phase, CurrentObjective);
        }

        public void SetPhase(ProtoPhase phase, string objective, string cardTitle, string cardBody, bool showCard = true)
        {
            CurrentPhase = phase;
            CurrentObjective = objective ?? "";

            ForceTimeNormal();
            ApplyInputGatesForPhase(phase);

            if (this.showPhaseCard && showCard)
            {
                var t = string.IsNullOrEmpty(cardTitle) ? PhaseLabel(phase) + " START" : cardTitle;
                var b = string.IsNullOrEmpty(cardBody) ? CurrentObjective : cardBody;
                ShowPhaseCard(t, b);
            }
        }


        private void ApplyInputGatesForPhase(ProtoPhase phase)
        {
            // Camera should remain controllable for readability.
            if (cameraRig != null) cameraRig.enabled = true;

            bool move = (phase == ProtoPhase.Investigation || phase == ProtoPhase.Combat);
            bool combat = (phase == ProtoPhase.Combat);

            if (player != null) player.enabled = move;
            if (playerCombat != null) playerCombat.enabled = combat;
            if (lockOn != null) lockOn.enabled = combat;

            // Cursor policy: unlock for Story/Negotiation/Result, lock during control phases.
            bool lockCursor = (phase == ProtoPhase.Investigation || phase == ProtoPhase.Combat);
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
        }

        private void ForceTimeNormal()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }

        private void ShowPhaseCard(ProtoPhase phase, string objective)
        {
            _cardVisible = true;
            _cardT = 0f;

            _cardTitle = $"PHASE START : {PhaseLabel(phase)}";
            _cardBody = string.IsNullOrEmpty(objective) ? "" : $"目的：{objective}";
        }

        private void ShowPhaseCard(string title, string body)
        {
            _cardVisible = true;
            _cardT = 0f;
            _cardTitle = string.IsNullOrEmpty(title) ? "PHASE START" : title;
            _cardBody = body ?? "";
        }

        private void Update()
        {
            // Fail-safe: if phase is not a "paused" phase but timeScale stuck at 0, recover.
            if (Time.timeScale == 0f)
            {
                // We never intend to freeze time in this prototype; recover.
                ForceTimeNormal();
            }

            if (_cardVisible)
            {
                _cardT += Time.unscaledDeltaTime;
                if (_cardT >= Mathf.Max(0.25f, phaseCardSeconds))
                    _cardVisible = false;
            }

            // If references were destroyed/rebuilt, re-acquire.
            if (player == null) AutoFindRefs();
        }

        private void OnGUI()
        {
            if (!_cardVisible) return;

            float a = 1f;
            // Fade in/out quickly
            float fade = 0.2f;
            if (_cardT < fade) a = Mathf.InverseLerp(0f, fade, _cardT);
            else if (_cardT > phaseCardSeconds - fade) a = Mathf.InverseLerp(phaseCardSeconds, phaseCardSeconds - fade, _cardT);

            var prev = GUI.color;
            var bg = new Color(0f, 0f, 0f, 0.75f * a);
            GUI.color = bg;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _flat);

            GUI.color = new Color(1f, 1f, 1f, a);
            float w = Mathf.Min(820f, Screen.width * 0.86f);
            float h = 180f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.45f;

            GUI.Label(new Rect(x, y, w, 60), _cardTitle, _title);
            if (!string.IsNullOrEmpty(_cardBody))
                GUI.Label(new Rect(x, y + 70, w, 70), _cardBody, _body);

            GUI.color = prev;
        }

#if UNITY_EDITOR
        /// <summary>
        /// One-shot scene setup helper. Safe to call from an Editor script.
        /// Sets the initial phase without triggering transitions/cards.
        /// </summary>
        public void SetInitialPhaseForEditor(ProtoPhase phase)
        {
            CurrentPhase = phase;
        }
#endif


        private static string PhaseLabel(ProtoPhase p)
        {
            switch (p)
            {
                case ProtoPhase.Story: return "STORY";
                case ProtoPhase.Investigation: return "INVESTIGATION";
                case ProtoPhase.Combat: return "COMBAT";
                case ProtoPhase.Negotiation: return "NEGOTIATION";
                case ProtoPhase.Result: return "RESULT";
                default: return p.ToString().ToUpperInvariant();
            }
        }
    }
}
