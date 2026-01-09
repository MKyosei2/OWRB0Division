using System;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Paper-theater story overlay with Skip.
    /// - Shows a short intro story (PHASE 1) at episode start.
    /// - Shows a short break story (PHASE 3) when investigation evidence becomes sufficient,
    ///   then advances into combat.
    ///
    /// No scene wiring needed: auto-spawns at runtime.
    /// </summary>
    public sealed class Proto_StoryOverlay : MonoBehaviour
    {
        private static Proto_StoryOverlay _instance;
        public static Proto_StoryOverlay Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("Proto_StoryOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Proto_StoryOverlay>();
        }

        private struct Page
        {
            public string title;
            public string body;
            public Page(string t, string b) { title = t; body = b; }
        }

        private EpisodeController _episode;
        private InvestigationManager _inv;

        private PlayerController _player;
        private ThirdPersonCameraRig _cam;

        private bool _visible;
        private int _pageIndex;
        private Page[] _pages;
        private Action _onClose;

        // per-run guards
        private bool _introShown;
        private bool _breakShown;

        // UI resources
        private Texture2D _flat;
        private GUIStyle _title, _body, _hint, _btn;

        private bool _savedPlayerEnabled, _savedCamEnabled, _savedUiEnabled;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            EnsureGui();
        }

        private void EnsureGui()
        {
            if (_flat == null)
            {
                _flat = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _flat.SetPixel(0, 0, Color.white);
                _flat.Apply();
            }

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _hint = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _btn = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16
            };
        }

        private void Update()
        {
            EnsureRefs();

            // If no episode running, reset guards
            var flow = GameFlowController.Instance;
            if (flow == null || flow.State != FlowState.Playing)
            {
                _introShown = false;
                _breakShown = false;
                if (_visible) HideInternal(false);
                return;
            }

            if (_episode == null || _episode.Current == null) return;

            // PHASE 1: Intro story card (episode start)
            if (!_introShown && _episode.PhaseIndex == 0 && _episode.Current.phaseType == EpisodePhaseType.Intro)
            {
                _introShown = true;
                Show(BuildIntroPages(), () =>
                {
                    // advance into Investigation
                    _episode?.NextPhase();
                });
                return;
            }

            // PHASE 3: Break story card (when evidence is enough)
            if (!_breakShown && _episode.Current.phaseType == EpisodePhaseType.Investigation)
            {
                if (_inv != null && _inv.CollectedCount >= _inv.TargetCount && _inv.TargetCount > 0)
                {
                    _breakShown = true;
                    Show(BuildBreakPages(_inv.CollectedCount, _inv.TargetCount), () =>
                    {
                        // advance into Combat
                        _episode?.NextPhase();
                    });
                    return;
                }
            }

            // Input shortcuts when visible
            if (_visible)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Skip();
                }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                {
                    Next();
                }
            }
        }

        private void EnsureRefs()
        {
            if (_episode == null) _episode = FindObjectOfType<EpisodeController>();
            if (_inv == null) _inv = InvestigationManager.Instance;

            if (_player == null) _player = FindObjectOfType<PlayerController>();
            if (_cam == null) _cam = FindObjectOfType<ThirdPersonCameraRig>();
        }

        private Page[] BuildIntroPages()
        {
            var flow = GameFlowController.Instance;
            var title = flow != null ? flow.subtitle : "CASE 01";
            return new[]
            {
                new Page(
                    title,
                    "終電後の駅で“声が消える”事件が続発している。\n" +
                    "現場に残るのは、破損した改札と記録の欠落だけ。\n\n" +
                    "あなたの任務：監視映像の断片を回収し、異界の“規約”を特定せよ。"
                ),
                new Page(
                    "目的",
                    "・調査フェーズで証拠を集める（目標：1〜2）\n" +
                    "・規約（戦闘ルール）を特定して、収束作戦へ\n\n" +
                    "操作：移動 / 調べる（E）"
                )
            };
        }

        private Page[] BuildBreakPages(int c, int t)
        {
            return new[]
            {
                new Page(
                    "証拠が揃った",
                    $"証拠を回収した（{c}/{t}）。\n" +
                    "改札の向こう側に“異界ホーム”が重なっている。\n\n" +
                    "規約が確定：同じ手を繰り返すな／視線を合わせるな（例）"
                ),
                new Page(
                    "次の目的",
                    "収束作戦：異界で対象をブレイクし、交渉に持ち込め。\n\n" +
                    "※スキップすると、すぐ戦闘へ移行します。"
                )
            };
        }

        private void Show(Page[] pages, Action onClose)
        {
            EnsureGui();
            _pages = pages ?? Array.Empty<Page>();
            _pageIndex = 0;
            _onClose = onClose;

            _visible = true;

            // Freeze player/camera/UI while story is on-screen
            EnsureRefs();
            _savedPlayerEnabled = (_player != null && _player.enabled);
            _savedCamEnabled = (_cam != null && _cam.enabled);

            if (_player != null) _player.enabled = false;
            if (_cam != null) _cam.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Next()
        {
            if (!_visible) return;
            if (_pages == null || _pages.Length == 0)
            {
                HideInternal(true);
                return;
            }

            _pageIndex++;
            if (_pageIndex >= _pages.Length)
            {
                HideInternal(true);
            }
        }

        private void Skip()
        {
            if (!_visible) return;
            HideInternal(true);
        }

        private void HideInternal(bool invokeClose)
        {
            _visible = false;

            // Restore components (if next phase re-enables, this is still safe)
            if (_player != null) _player.enabled = _savedPlayerEnabled;
            if (_cam != null) _cam.enabled = _savedCamEnabled;

            // Cursor will be set by Flow/PhaseDirector; keep it conservative here.
            var flow = GameFlowController.Instance;
            if (flow != null && flow.State == FlowState.Playing)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (invokeClose)
            {
                var cb = _onClose;
                _onClose = null;
                cb?.Invoke();
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureGui();

            // backdrop
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _flat);
            GUI.color = prev;

            float w = Mathf.Min(920f, Screen.width * 0.92f);
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.18f;

            // content panel
            var panel = new Rect(x, y, w, Screen.height * 0.62f);
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(panel, _flat);
            GUI.color = prev;

            var p = (_pages != null && _pages.Length > 0)
                ? _pages[Mathf.Clamp(_pageIndex, 0, _pages.Length - 1)]
                : new Page("STORY", "");

            GUI.Label(new Rect(panel.x + 22, panel.y + 18, panel.width - 44, 44), p.title, _title);
            GUI.Label(new Rect(panel.x + 22, panel.y + 70, panel.width - 44, panel.height - 160), p.body, _body);

            string hint = "Enter/Space/Click：次へ    Esc：スキップ";
            string page = (_pages != null && _pages.Length > 0) ? $"({_pageIndex + 1}/{_pages.Length})" : "";
            GUI.Label(new Rect(panel.x + 22, panel.yMax - 78, panel.width - 44, 22), hint + "  " + page, _hint);

            // buttons
            float bw = 140f;
            float bh = 34f;
            float by = panel.yMax - 44f;
            float bx2 = panel.xMax - 22f - bw;
            float bx1 = bx2 - 12f - bw;

            if (GUI.Button(new Rect(bx1, by, bw, bh), "スキップ", _btn))
            {
                Skip();
            }

            string nextLabel = (_pages != null && _pages.Length > 0 && _pageIndex >= _pages.Length - 1) ? "続ける" : "次へ";
            if (GUI.Button(new Rect(bx2, by, bw, bh), nextLabel, _btn))
            {
                Next();
            }
        }
    }
}
