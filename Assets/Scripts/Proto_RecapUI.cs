using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Simple recap card shown on resume. AI-free.
    /// Blocks input until dismissed.
    /// </summary>
    public class ProtoRecapUI : MonoBehaviour
    {
        public static ProtoRecapUI Instance { get; private set; }

        /// <summary>
        /// True while the recap card is on screen and input is intentionally blocked.
        /// </summary>
        public bool IsVisible => _visible;

        [Header("Visibility")]
        public bool showOnStartIfSaveExists = false;

        private bool _visible;
        private ProtoRecapDatabase.RecapContent _content;
        private ProtoSaveState _state;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _accent;
        private Texture2D _tex;

        private float _prevTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            DontDestroyOnLoad(gameObject);

            _tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();

            BuildStyles();
        }

        private void Start()
        {
            if (!showOnStartIfSaveExists) return;
            var st = ProtoSaveSystem.Load();
            if (st != null && st.wasInterrupted)
                Show(st);
        }

        public void Show(ProtoSaveState state)
        {
            _state = state;
            _content = ProtoRecapDatabase.GetRecap(state);
            _visible = true;

            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void HideAndResume(bool warpToObjective)
        {
            _visible = false;
            RestoreTimeScale();

            // Mark as no longer "interrupted" so it won't repeat every boot.
            if (_state != null)
            {
                _state.wasInterrupted = false;
                ProtoSaveSystem.Save(_state);
            }

            // Optional warp: for prototype, resume is usually enough.
            if (warpToObjective)
            {
                var warper = FindObjectOfType<ProtoCheckpointWarp>();
                if (warper != null) warper.WarpToNextObjective(_state);
            }
        }

        private void RestoreTimeScale()
        {
            // Guard against leaving the game paused if this UI is destroyed or disabled.
            Time.timeScale = (_prevTimeScale <= 0f) ? 1f : _prevTimeScale;
        }

        private void OnDisable()
        {
            if (_visible) RestoreTimeScale();
        }

        private void OnDestroy()
        {
            if (_visible) RestoreTimeScale();
        }

        private void BuildStyles()
        {
            _title = new GUIStyle
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _title.normal.textColor = new Color(0.92f, 0.95f, 0.98f, 1f);

            _body = new GUIStyle
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _body.normal.textColor = new Color(0.92f, 0.95f, 0.98f, 0.92f);

            _accent = new GUIStyle(_body);
            _accent.fontStyle = FontStyle.Bold;
            _accent.normal.textColor = new Color(0.25f, 0.90f, 0.85f, 1f);
        }

        private void OnGUI()
        {
            if (!_visible || _content == null) return;

            var sw = Screen.width;
            var sh = Screen.height;

            // Backdrop
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _tex);

            // Card
            float w = Mathf.Min(760, sw * 0.92f);
            float h = Mathf.Min(420, sh * 0.82f);
            float x = (sw - w) * 0.5f;
            float y = (sh - h) * 0.5f;

            GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), _tex);

            float pad = 18f;
            float cx = x + pad;
            float cy = y + pad;

            GUI.color = Color.white;
            GUI.Label(new Rect(cx, cy, w - pad * 2, 28), _content.caseTitle, _title);
            cy += 36;

            // Summary
            if (_content.summaryLines != null)
            {
                for (int i = 0; i < _content.summaryLines.Length && i < 3; i++)
                {
                    GUI.Label(new Rect(cx, cy, w - pad * 2, 24), "・" + _content.summaryLines[i], _body);
                    cy += 22;
                }
            }

            cy += 6;
            GUI.Label(new Rect(cx, cy, w - pad * 2, 24), _content.objectiveLine ?? "", _accent);
            cy += 30;

            // Tags
            if (_content.ruleTags != null && _content.ruleTags.Length > 0)
            {
                GUI.Label(new Rect(cx, cy, w - pad * 2, 22), "規約タグ：" + string.Join(" / ", _content.ruleTags), _body);
                cy += 26;
            }

            // Controls
            if (_content.controlHints != null)
            {
                GUI.Label(new Rect(cx, cy, w - pad * 2, 22), "操作の要点：", _body);
                cy += 22;
                for (int i = 0; i < _content.controlHints.Length && i < 3; i++)
                {
                    GUI.Label(new Rect(cx + 16, cy, w - pad * 2 - 16, 22), "- " + _content.controlHints[i], _body);
                    cy += 20;
                }
            }

            // Buttons
            float bw = 180;
            float bh = 36;
            float by = y + h - pad - bh;
            float bx2 = x + w - pad - bw;
            float bx1 = bx2 - 14 - bw;

            GUI.color = Color.white;
            if (GUI.Button(new Rect(bx1, by, bw, bh), "再開"))
            {
                HideAndResume(false);
            }
            if (GUI.Button(new Rect(bx2, by, bw, bh), "目的地点へ移動"))
            {
                HideAndResume(true);
            }

            // ESC closes
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                HideAndResume(false);
                Event.current.Use();
            }
        }
    }
}
