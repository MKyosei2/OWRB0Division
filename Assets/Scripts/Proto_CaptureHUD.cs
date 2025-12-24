using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// ^F^CR[h/Take/ȈFPS/Be`FbN\B
    /// DebugHUDƂ͕ʂɁuȍvB
    /// </summary>
    public class Proto_CaptureHUD : MonoBehaviour
    {
        [Header("Take")]
        public int takeNumber = 1;
        public KeyCode takeUpKey = KeyCode.F7;
        public KeyCode takeDownKey = KeyCode.F8;

        [Header("Display")]
        public bool show = true;

        private float _fps;
        private float _fpsAcc;
        private int _fpsFrames;
        private float _fpsTimer;

        private GUIStyle _small;
        private GUIStyle _big;

        private Texture2D _tex;

        private GameFlowController _flow;
        private DebugHUD _hud;
        private float _refT;

        private void Awake()
        {
            _tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();

            _small = new GUIStyle()
            {
                fontSize = 12,
                wordWrap = false,
                alignment = TextAnchor.UpperLeft
            };
            _small.normal.textColor = new Color(0.92f, 0.95f, 0.98f, 0.92f);

            _big = new GUIStyle()
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                alignment = TextAnchor.UpperLeft
            };
            _big.normal.textColor = new Color(0.92f, 0.95f, 0.98f, 1f);
        }

        private void Update()
        {
            _refT -= Time.unscaledDeltaTime;
            if (_refT <= 0f)
            {
                if (_flow == null) _flow = FindObjectOfType<GameFlowController>();
                if (_hud == null) _hud = FindObjectOfType<DebugHUD>();
                _refT = 0.5f;
            }

            if (Input.GetKeyDown(takeUpKey)) { takeNumber++; SubtitleManager.Instance?.Add($"yTAKEz{takeNumber}", 1.0f); }
            if (Input.GetKeyDown(takeDownKey)) { takeNumber = Mathf.Max(1, takeNumber - 1); SubtitleManager.Instance?.Add($"yTAKEz{takeNumber}", 1.0f); }

            // ȈFPSi0.5bXVj
            _fpsTimer += Time.unscaledDeltaTime;
            _fpsAcc += 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _fpsFrames++;

            if (_fpsTimer >= 0.5f)
            {
                _fps = _fpsAcc / Mathf.Max(1, _fpsFrames);
                _fpsTimer = 0f;
                _fpsAcc = 0f;
                _fpsFrames = 0;
            }
        }

        private void OnGUI()
        {
            if (!show) return;

            if (_flow != null && _flow.State != FlowState.Playing) return;

            // ^CR[h
            float t = Time.timeSinceLevelLoad;
            string tc = FormatTimecode(t);

            string header = $"TAKE {takeNumber:00}   TC {tc}";
            string info = $"{Screen.width}x{Screen.height}   FPS {Mathf.RoundToInt(_fps)}";

            // E
            float w = 320f, h = 54f;
            Rect r = new Rect(Screen.width - w - 12f, 12f, w, h);
            Panel(r);
            GUI.Label(new Rect(r.x + 12, r.y + 10, r.width - 24, 18), header, _big);
            GUI.Label(new Rect(r.x + 12, r.y + 30, r.width - 24, 18), info, _small);

            // EFBe`FbN
            var hud = _hud;
            var sub = SubtitleManager.Instance;
            bool capture = (hud != null && hud.captureMode);
            bool letter = (hud != null && hud.letterbox);
            bool subsOn = (sub != null && sub.enabledSubtitles);
            bool audioOn = (AudioListener.volume > 0.001f);

            string checklist =
                $"CAPTURE : {(capture ? "OK" : "NG")}\n" +
                $"LETTER  : {(letter ? "OK" : "NG")}\n" +
                $"SUB     : {(subsOn ? "OK" : "NG")}\n" +
                $"AUDIO   : {(audioOn ? "OK" : "NG")}";

            float w2 = 210f, h2 = 90f;
            Rect r2 = new Rect(Screen.width - w2 - 12f, Screen.height - h2 - 12f, w2, h2);
            Panel(r2);
            GUI.Label(new Rect(r2.x + 12, r2.y + 10, r2.width - 24, r2.height - 20), checklist, _small);
        }

        private void Panel(Rect r)
        {
            var prev = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.03f, 0.75f);
            GUI.DrawTexture(r, _tex);
            GUI.color = prev;

            // ANZg
            var prev2 = GUI.color;
            GUI.color = new Color(0.25f, 0.90f, 0.85f, 1f);
            GUI.DrawTexture(new Rect(r.x, r.y, 4, r.height), _tex);
            GUI.color = prev2;
        }

        private static string FormatTimecode(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int mm = total / 60;
            int ss = total % 60;

            int ff = Mathf.FloorToInt((seconds - total) * 30f); // 30fpsz̊Ȉ
            ff = Mathf.Clamp(ff, 0, 29);

            return $"{mm:00}:{ss:00}:{ff:00}";
        }
    }
}
