using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Lightweight HUD to make current phase obvious.
    /// Draws: PHASE label + objective (top-left) and an optional step bar.
    /// </summary>
    public class Proto_PhaseHUD : MonoBehaviour
    {
        public bool showStepBar = true;
        public bool showObjective = true;

        [Header("Layout")]
        public Vector2 topLeft = new Vector2(18, 14);
        public float width = 460f;

        private ProtoPhaseDirector _director;
        private GUIStyle _phaseStyle;
        private GUIStyle _objectiveStyle;
        private GUIStyle _stepStyle;
        private Texture2D _flat;

        private readonly Color _panel = new Color(0.05f, 0.06f, 0.08f, 0.75f);
        private readonly Color _accent = new Color(0.25f, 0.90f, 0.85f, 1f);
        private readonly Color _text = new Color(0.92f, 0.95f, 0.98f, 1f);

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureResources();
        }

        private void EnsureResources()
        {
            if (_flat == null)
            {
                _flat = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _flat.SetPixel(0, 0, Color.white);
                _flat.Apply();
            }

            _phaseStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _phaseStyle.normal.textColor = _text;

            _objectiveStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _objectiveStyle.normal.textColor = new Color(_text.r, _text.g, _text.b, 0.92f);

            _stepStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };
            _stepStyle.normal.textColor = new Color(_text.r, _text.g, _text.b, 0.78f);
        }

        private void Update()
        {
            if (_director == null) _director = ProtoPhaseDirector.Instance;
        }

        private void OnGUI()
        {
            if (_director == null) return;

            float x = topLeft.x;
            float y = topLeft.y;

            var phaseName = _director.CurrentPhase.ToString().ToUpperInvariant();
            var phaseLine = $"PHASE : {phaseName}";
            var objective = _director.CurrentObjective ?? "";

            float h = 54f;
            if (showObjective && !string.IsNullOrEmpty(objective)) h += 26f;
            if (showStepBar) h += 18f;

            var rect = new Rect(x, y, width, h);
            DrawRect(rect, _panel);

            float ty = y + 8f;
            GUI.Label(new Rect(x + 10f, ty, width - 20f, 22f), phaseLine, _phaseStyle);

            // Accent bar
            DrawRect(new Rect(x, y, 4f, h), _accent);

            ty += 22f;

            if (showObjective && !string.IsNullOrEmpty(objective))
            {
                GUI.Label(new Rect(x + 10f, ty, width - 20f, 44f), $"目的：{objective}", _objectiveStyle);
                ty += 26f;
            }

            if (showStepBar)
            {
                GUI.Label(new Rect(x + 10f, ty, width - 20f, 16f), BuildStepBar(_director.CurrentPhase), _stepStyle);
            }
        }

        /// <summary>
        /// One-shot setup helper. This HUD is OnGUI-based, so we just ensure resources exist.
        /// </summary>
        public void EnsureMinimalUI()
        {
            EnsureResources();
        }


        private static string BuildStepBar(ProtoPhase current)
        {
            // Simple text bar; avoids heavy UI work.
            string Mark(ProtoPhase p) => p == current ? $"[{p}]" : p.ToString();
            return $"{Mark(ProtoPhase.Story)} → {Mark(ProtoPhase.Investigation)} → {Mark(ProtoPhase.Combat)} → {Mark(ProtoPhase.Negotiation)} → {Mark(ProtoPhase.Result)}";
        }

        private void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _flat);
            GUI.color = old;
        }
    }
}
