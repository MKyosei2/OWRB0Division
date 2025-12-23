using System.Collections.Generic;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// éBâeéñåÃñhé~ÅF
    /// - CaptureMode / Letterbox / Subtitle / AudioVolume Çäƒéã
    /// - NGÇ»ÇÁê‘åxçêÅAê›íËÇ™ãñÇ≥ÇÍÇÈÇ»ÇÁé©ìÆèCê≥
    /// </summary>
    public class Proto_CaptureGuard : MonoBehaviour
    {
        [Header("Toggle")]
        public bool guardEnabled = true;
        public bool autoFix = true;
        public KeyCode toggleGuardKey = KeyCode.F12;

        [Header("Enforce While Playing")]
        public bool enforceOnlyWhilePlaying = true;

        [Header("Enforce Targets")]
        public bool requireCaptureMode = true;
        public bool requireLetterbox = true;
        public bool requireSubtitles = true;
        public bool requireAudio = true;

        [Header("Audio")]
        [Range(0f, 1f)] public float minAudioVolume = 0.05f;
        public bool autoRestoreAudioVolume = true;
        public float restoreAudioVolume = 1.0f;

        [Header("Performance (Optional)")]
        public bool enforce60fps = true;
        public int targetFrameRate = 60;
        public bool disableVsync = true;

        private readonly List<string> _warnings = new List<string>(8);

        private Texture2D _flat;
        private GUIStyle _warnStyle;
        private GUIStyle _smallStyle;

        private DebugHUD _hud;
        private GameFlowController _flow;
        private SubtitleManager _sub;

        private void Awake()
        {
            _flat = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _flat.SetPixel(0, 0, Color.white);
            _flat.Apply();

            _warnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            _warnStyle.normal.textColor = new Color(1f, 0.35f, 0.35f, 1f);

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            _smallStyle.normal.textColor = new Color(1f, 0.7f, 0.7f, 0.95f);

            ApplyPerfSettings();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleGuardKey))
            {
                guardEnabled = !guardEnabled;
                SubtitleManager.Instance?.Add($"ÅyCAPTURE GUARDÅz{(guardEnabled ? "ON" : "OFF")}", 1.4f);
            }

            if (!guardEnabled) return;

            RefreshRefs();

            if (enforceOnlyWhilePlaying && _flow != null && _flow.State != FlowState.Playing)
                return;

            _warnings.Clear();

            // CaptureMode / Letterbox
            if (_hud != null)
            {
                if (requireCaptureMode && !_hud.captureMode)
                {
                    _warnings.Add("CAPTURE MODE Ç™ OFF");
                    if (autoFix) { _hud.captureMode = true; ToastFix("CaptureMode ON"); }
                }

                if (requireLetterbox && !_hud.letterbox)
                {
                    _warnings.Add("LETTERBOX Ç™ OFF");
                    if (autoFix) { _hud.letterbox = true; ToastFix("Letterbox ON"); }
                }
            }
            else
            {
                _warnings.Add("DebugHUD Ç™å©Ç¬Ç©ÇÁÇ»Ç¢ÅiHUDñ¢ê∂ê¨ÅHÅj");
            }

            // Subtitles
            if (requireSubtitles)
            {
                if (_sub == null) _sub = SubtitleManager.Instance;
                if (_sub == null)
                {
                    _warnings.Add("SubtitleManager Ç™å©Ç¬Ç©ÇÁÇ»Ç¢");
                }
                else
                {
                    // enabledSubtitles Ç™ë∂ç›Ç∑ÇÈëOíÒÅiä˘Ç…ëºÇÃÉXÉNÉäÉvÉgÇ≈Ç‡éQè∆ÇµÇƒÇ¢ÇÈÇΩÇﬂÅj
                    if (!_sub.enabledSubtitles)
                    {
                        _warnings.Add("SUBTITLE Ç™ OFF");
                        if (autoFix) { _sub.enabledSubtitles = true; ToastFix("Subtitle ON"); }
                    }
                }
            }

            // Audio
            if (requireAudio)
            {
                float v = AudioListener.volume;
                if (v < minAudioVolume)
                {
                    _warnings.Add($"AUDIO Ç™è¨Ç≥Ç∑Ç¨ÇÈÅi{v:0.00}Åj");
                    if (autoFix && autoRestoreAudioVolume)
                    {
                        AudioListener.volume = restoreAudioVolume;
                        ToastFix($"Audio Volume {restoreAudioVolume:0.00}");
                    }
                }
            }

            // Perf (Optional)
            if (enforce60fps)
                ApplyPerfSettings();
        }

        private void ApplyPerfSettings()
        {
            if (disableVsync) QualitySettings.vSyncCount = 0;
            if (enforce60fps) Application.targetFrameRate = targetFrameRate;
        }

        private void ToastFix(string msg)
        {
            // éöñãÇâòÇµÇ∑Ç¨Ç»Ç¢ÇÊÇ§íZÇ≠
            if (SubtitleManager.Instance != null)
                SubtitleManager.Instance.Add($"Å¶AutoFix: {msg}", 1.0f);
        }

        private void RefreshRefs()
        {
            if (_hud == null) _hud = FindObjectOfType<DebugHUD>();
            if (_flow == null) _flow = FindObjectOfType<GameFlowController>();
            if (_sub == null) _sub = SubtitleManager.Instance;
        }

        private void OnGUI()
        {
            if (!guardEnabled) return;
            if (_warnings.Count == 0) return;

            // è„ïîíÜâõÇ…ê‘åxçêÅiò^âÊÇ…Ç‡ì¸ÇÈÅÅéñåÃÇ…ãCÇ√ÇØÇÈÅj
            float w = Mathf.Min(920f, Screen.width - 40f);
            float h = 72f;
            float x = (Screen.width - w) * 0.5f;
            float y = 12f;

            var prev = GUI.color;
            GUI.color = new Color(0.05f, 0.02f, 0.02f, 0.75f);
            GUI.DrawTexture(new Rect(x, y, w, h), _flat);
            GUI.color = prev;

            GUI.color = new Color(1f, 0.35f, 0.35f, 1f);
            GUI.DrawTexture(new Rect(x, y + h - 3f, w, 3f), _flat);
            GUI.color = prev;

            string head = "CAPTURE WARNING";
            string body = string.Join(" / ", _warnings);

            GUI.Label(new Rect(x + 12f, y + 10f, w - 24f, 22f), head, _warnStyle);
            GUI.Label(new Rect(x + 12f, y + 34f, w - 24f, 22f), body, _smallStyle);
        }
    }
}
