using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Minimal cinematic wrapper for prototype demos.
    /// Auto-spawns at runtime and shows:
    ///  - Intro card when Flow enters Playing
    ///  - Outro card when Flow enters Completed (short, then the existing Completed UI remains)
    /// This avoids scene wiring and keeps the prototype "showable" for P/D.
    /// </summary>
    public class Proto_IntroOutroOverlay : MonoBehaviour
    {
        private GameFlowController _flow;
        private FlowState _lastState;

        

        private NegotiationOutcome _lastOutcome = NegotiationOutcome.None;
// Intro/outro state
        private bool _showIntro;
        private bool _showOutro;
        private float _t;             // elapsed within current card
        private float _holdSeconds = 4.5f;
        private float _fadeSeconds = 0.5f;

        // Styles
        private GUIStyle _title;
        private GUIStyle _subtitle;
        private GUIStyle _body;
        private GUIStyle _hint;
        private Texture2D _flat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // Avoid duplicates across domain reload / scene loads.
            if (FindObjectOfType<Proto_IntroOutroOverlay>() != null) return;

            var go = new GameObject("Proto_IntroOutroOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<Proto_IntroOutroOverlay>();
        }

        private void Awake()
        {
            _lastState = FlowState.Title;
            EnsureResources();
        }

        private void OnEnable()
        {
            if (EventBus.Instance != null) EventBus.Instance.OnEpisodeComplete += OnEpisodeComplete;
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null) EventBus.Instance.OnEpisodeComplete -= OnEpisodeComplete;
        }

        private void OnEpisodeComplete(NegotiationOutcome o)
        {
            _lastOutcome = o;
        }


        private void EnsureResources()
        {
            if (_flat == null)
            {
                _flat = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _flat.SetPixel(0, 0, Color.white);
                _flat.Apply();
            }

            // No GUI.skin access here; safe outside OnGUI.
            _title = new GUIStyle
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                wordWrap = true
            };
            _subtitle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.92f) },
                wordWrap = true
            };
            _body = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.92f) },
                wordWrap = true
            };
            _hint = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.65f) },
                wordWrap = true
            };
        }

        private void Update()
        {
            if (_flow == null) _flow = GameFlowController.Instance != null ? GameFlowController.Instance : FindObjectOfType<GameFlowController>();
            if (_flow == null) return;

            // Detect transitions
            if (_flow.State != _lastState)
            {
                var prev = _lastState;
                _lastState = _flow.State;

                if (_lastState == FlowState.Playing)
                {
                    // Show intro at the beginning of a run
                    _showIntro = true;
                    _showOutro = false;
                    _t = 0f;
                }
                else if (_lastState == FlowState.Completed && prev == FlowState.Playing)
                {
                    // Brief outro card when completed
                    _showOutro = true;
                    _showIntro = false;
                    _t = 0f;
                }
            }

            // Progress timer
            if (_showIntro || _showOutro) _t += Time.unscaledDeltaTime;

            // Skip
            if ((_showIntro || _showOutro) && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return)))
            {
                _showIntro = false;
                _showOutro = false;
            }

            // Auto-hide after hold time (+ fades)
            var total = _fadeSeconds + _holdSeconds + _fadeSeconds;
            if ((_showIntro || _showOutro) && _t >= total)
            {
                _showIntro = false;
                _showOutro = false;
            }
        }
        private string OutcomeBody()
        {
            // Keep as a single-line friendly string; use explicit \n if needed.
            switch (_lastOutcome)
            {
                case NegotiationOutcome.Truce:
                    return @"停戦は成立した。期限付きの合意は、次の火種も孕む。
規約を守り、現場を無事に閉じた。";
                case NegotiationOutcome.Contract:
                    return @"契約は成立した。対価は支払われ、力は味方になる。
ただし“約束”は、破れば必ず返ってくる。";
                case NegotiationOutcome.Seal:
                    return @"封印は完了した。再発は抑えられたが、管理コストが残る。
規約は静かに閉じられた。";
                case NegotiationOutcome.Slay:
                    return @"討伐で収束した。最短の解決だが、残るものもある。
現場は守れた。";
                case NegotiationOutcome.None:
                default:
                    return @"収束は未達だ。条件を見直し、次の一手を探せ。
規約はまだ、こちらを試している。";
            }
        }



        private void OnGUI()
        {
            if (!_showIntro && !_showOutro) return;
            if (_flow == null) return;

            // Compute alpha with fade-in/out.
            float alpha;
            if (_t < _fadeSeconds) alpha = Mathf.Clamp01(_t / _fadeSeconds);
            else if (_t > _fadeSeconds + _holdSeconds) alpha = Mathf.Clamp01(1f - ((_t - (_fadeSeconds + _holdSeconds)) / _fadeSeconds));
            else alpha = 1f;

            // Backdrop
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.80f * alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _flat);
            GUI.color = prev;

            // Text block
            float w = Mathf.Min(900f, Screen.width * 0.92f);
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.38f;
            float h = Screen.height * 0.40f;

            var title = _showIntro ? _flow.gameTitle : "DEBRIEF";
            var sub = _showIntro ? _flow.subtitle : ($"結末：{_lastOutcome}");
            var body = _showIntro ? _flow.conceptLine : OutcomeBody();
            var hint = _showIntro ? "Space / Click で開始（スキップ可）" : "Space / Click で閉じる";

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(x, y - 80, w, 70), title, _title);
            GUI.Label(new Rect(x, y, w, 28), sub, _subtitle);
            GUI.Label(new Rect(x, y + 44, w, 80), body, _body);
            GUI.Label(new Rect(x, y + 140, w, 26), hint, _hint);
            GUI.color = prev;
        }
    }
}