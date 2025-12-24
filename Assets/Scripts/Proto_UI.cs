using UnityEngine;

namespace OJikaProto
{
    public class DebugHUD : MonoBehaviour
    {
        [Header("Feedback (Optional)")]
        public AudioClip ruleViolationSfx;
        public float flashFadeSpeed = 2.4f;

        [Header("Time Distortion on Violation")]
        [Range(0.05f, 1f)] public float violationSlowScale = 0.25f;
        [Range(0.02f, 0.6f)] public float violationSlowDuration = 0.12f;
        public float timeRecoverSpeed = 6.0f;

        [Header("Objective Navi (Bottom-Right)")]
        public bool showObjectiveNavi = true;

        [Header("Combat Readability")]
        public bool showEnemyGauges = true;
        public bool showNegotiationPrompt = true;
        public bool showRuleWarnings = true;

        [Header("Capture / Cinematic")]
        public bool captureMode = false;
        public bool letterbox = true;

        [Header("Summary Overlay")]
        public bool showSummaryOverlay = false;

        private AudioSource _audio;
        private Texture2D _flatTex;

        private float _flashA;
        private string _enemyLine;
        private float _enemyLineT;

        private string _toast;
        private float _toastT;

        private EpisodeController _episode;
        private GameFlowController _flow;

        private float _baseFixedDelta;
        private float _slowT;
        private bool _recovering;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _smallStyle;

        private Color _panel = new Color(0.05f, 0.06f, 0.08f, 0.88f);
        private Color _panel2 = new Color(0.02f, 0.02f, 0.03f, 0.75f);
        private Color _accent = new Color(0.25f, 0.90f, 0.85f, 1f);
        private Color _text = new Color(0.92f, 0.95f, 0.98f, 1f);

        // Summary text cached
        private string _summaryText = "";

        // -------------------- Intro Help Overlay --------------------
        [Header("Intro Help (Demo)")]
        public bool introHelpEnabled = true;
        public KeyCode introToggleKey = KeyCode.H;
        public KeyCode introDismissKey = KeyCode.Escape;
        public KeyCode introAdvanceKey = KeyCode.Space;
        [Tooltip("Pinned mode: keep showing even after steps end")]
        public bool introStartPinned = false;

        private bool _showIntroHelp;
        private bool _introPinned;
        private int _introStep;
        private float _introAutoHideT;
        private bool _introShownOnce;

        private void Awake()
        {
            CoreEnsure.EnsureAll();

            _episode = FindObjectOfType<EpisodeController>();
            _flow = FindObjectOfType<GameFlowController>();

            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;

            _flatTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _flatTex.SetPixel(0, 0, Color.white);
            _flatTex.Apply();

            _baseFixedDelta = Time.fixedDeltaTime;

            BuildStyles();

            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnToast += (msg) => { _toast = msg; _toastT = 2f; };
                EventBus.Instance.OnRuleViolation += OnRuleViolation;
            }

            // Intro Help init
            _introPinned = introStartPinned;
            _showIntroHelp = false;
            _introStep = 0;
            _introAutoHideT = 0f;
            _introShownOnce = false;

        }

        private void BuildStyles()
        {
            // IMPORTANT: Do NOT touch GUI.skin here.
            // GUI.* / GUI.skin are only valid during OnGUI events.
            // Awake/Start/Update must avoid them.

            _titleStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _titleStyle.normal.textColor = _text;

            _bodyStyle = new GUIStyle
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _bodyStyle.normal.textColor = _text;

            _smallStyle = new GUIStyle
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _smallStyle.normal.textColor = new Color(_text.r, _text.g, _text.b, 0.86f);
        }

        public void ToggleCaptureMode() => captureMode = !captureMode;

        /// <summary>
        /// デモ終了時に呼ばれる：サマリー表示をON、内容を更新
        /// </summary>
        public void ShowRunSummary(string summaryText)
        {
            _summaryText = summaryText ?? "";
            showSummaryOverlay = true;
        }

        public void HideRunSummary()
        {
            showSummaryOverlay = false;
        }

        private void OnDestroy()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnRuleViolation -= OnRuleViolation;

            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }

        private void OnRuleViolation(RuleViolationSignal sig)
        {
            _flashA = Mathf.Max(_flashA, Mathf.Clamp01(sig.intensity) * 0.9f);
            _enemyLine = MakeEnemyLine(sig.ruleName);
            _enemyLineT = 2.0f;

            _slowT = Mathf.Max(_slowT, violationSlowDuration);
            _recovering = false;

            if (ruleViolationSfx != null && _audio != null)
                _audio.PlayOneShot(ruleViolationSfx, 1f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) { captureMode = !captureMode; SubtitleManager.Instance?.Add($"【CAPTURE】{(captureMode ? "ON" : "OFF")}", 1.2f); }
            if (Input.GetKeyDown(KeyCode.F10)) { letterbox = !letterbox; SubtitleManager.Instance?.Add($"【CINEMA】{(letterbox ? "ON" : "OFF")}", 1.2f); }

            // サマリー表示を閉じる
            if (showSummaryOverlay && Input.GetKeyDown(KeyCode.Escape))
                showSummaryOverlay = false;

            if (_toastT > 0f) _toastT -= Time.deltaTime;

            if (_flashA > 0f)
                _flashA = Mathf.MoveTowards(_flashA, 0f, flashFadeSpeed * Time.deltaTime);

            if (_enemyLineT > 0f) _enemyLineT -= Time.deltaTime;

            if (_flow == null) _flow = FindObjectOfType<GameFlowController>();


            // ---- Intro Help (state/input only; no GUI calls here) ----
            if (introHelpEnabled)
            {
                // Auto show once when entering Playing
                if (!_introShownOnce && _flow != null && _flow.State == FlowState.Playing)
                {
                    _introShownOnce = true;
                    _showIntroHelp = true;
                    _introStep = 0;
                    _introAutoHideT = 0f;
                }

                if (Input.GetKeyDown(introToggleKey))
                {
                    _introPinned = !_introPinned;
                    _showIntroHelp = _introPinned || _showIntroHelp;
                    _introAutoHideT = 0f;
                }

                if (_showIntroHelp)
                {
                    if (!_introPinned)
                    {
                        _introAutoHideT += Time.unscaledDeltaTime;
                        if (_introAutoHideT > 18f) _showIntroHelp = false;
                    }

                    if (Input.GetKeyDown(introDismissKey))
                    {
                        _showIntroHelp = false;
                        _introPinned = false;
                    }

                    if (Input.GetKeyDown(introAdvanceKey) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return))
                    {
                        _introStep = Mathf.Min(_introStep + 1, 3);
                        _introAutoHideT = 0f;
                        if (_introStep >= 3 && !_introPinned) _showIntroHelp = false;
                    }
                }
            }

            UpdateTimeDistortion();
        }

        private void UpdateTimeDistortion()
        {
            if (_flow != null && _flow.State != FlowState.Playing)
            {
                if (Time.timeScale != 1f)
                {
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = _baseFixedDelta;
                }
                _slowT = 0f;
                _recovering = false;
                return;
            }

            if (_slowT > 0f)
            {
                _slowT -= Time.unscaledDeltaTime;
                Time.timeScale = violationSlowScale;
                Time.fixedDeltaTime = _baseFixedDelta * Time.timeScale;
                if (_slowT <= 0f) _recovering = true;
                return;
            }

            if (_recovering)
            {
                float ts = Mathf.MoveTowards(Time.timeScale, 1f, timeRecoverSpeed * Time.unscaledDeltaTime);
                Time.timeScale = ts;
                Time.fixedDeltaTime = _baseFixedDelta * Time.timeScale;

                if (Mathf.Abs(Time.timeScale - 1f) < 0.001f)
                {
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = _baseFixedDelta;
                    _recovering = false;
                }
            }
        }

        private void OnGUI()
        {
            // レターボックス（映像向け）
            if (letterbox && _flow != null && _flow.State == FlowState.Playing)
            {
                DrawRect(new Rect(0, 0, Screen.width, 70), Color.black);
                DrawRect(new Rect(0, Screen.height - 70, Screen.width, 70), Color.black);
            }

            // 規約違反フラッシュ
            if (_flashA > 0.001f)
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 0f, 0f, _flashA);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _flatTex);
                GUI.color = prev;
            }

            if (_flow != null)
            {
                if (_flow.State == FlowState.Title) { DrawTitleScreen(_flow); return; }
                if (_flow.State == FlowState.Completed) { DrawCompletedScreen(_flow); return; }
            }

            // 戦闘可読性
            if (showEnemyGauges) DrawEnemyGauges();
            if (showNegotiationPrompt) DrawNegotiationPrompt();

            // 字幕（映像の軸）
            DrawSubtitles();

            if (!captureMode)
            {
                DrawTopLeftPhase();
                DrawRulesPanel();
                DrawInfiltrationHUD();
                if (showRuleWarnings) DrawRuleWarnings();
                DrawNegotiationPanel();
                DrawRunLog();
                if (showObjectiveNavi) DrawObjectiveNavi();

                if (_toastT > 0f && !string.IsNullOrEmpty(_toast))
                    DrawToast(_toast);
            }

            // 怪異の台詞（中央上）
            if (_enemyLineT > 0f && !string.IsNullOrEmpty(_enemyLine))
                DrawEnemyLine(_enemyLine);

            // ✅ 提出向け：サマリーオーバーレイ
            if (showSummaryOverlay && !string.IsNullOrEmpty(_summaryText))
                DrawSummaryOverlay(_summaryText);
        }

        // -------------------- Summary Overlay --------------------
        private void DrawSummaryOverlay(string text)
        {
            float w = Mathf.Min(920f, Screen.width - 40f);
            float h = 360f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            DrawRect(new Rect(x, y, w, h), _panel);
            DrawRect(new Rect(x, y, 4, h), _accent);

            GUI.Label(new Rect(x + 16, y + 14, w - 32, 24), "RUN SUMMARY (For Review)", _titleStyle);
            GUI.Label(new Rect(x + 16, y + 48, w - 32, h - 88), text, _bodyStyle);
            GUI.Label(new Rect(x + 16, y + h - 30, w - 32, 22), "Esc：閉じる", _smallStyle);
        }

        // -------------------- Theme Helpers --------------------
        private void DrawRect(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _flatTex);
            GUI.color = prev;
        }

        private void DrawPanel(Rect r, string header, string body, bool accent = true)
        {
            DrawRect(r, _panel);
            if (accent) DrawRect(new Rect(r.x, r.y, 4, r.height), _accent);

            GUI.Label(new Rect(r.x + 12, r.y + 10, r.width - 24, 24), header, _smallStyle);
            GUI.Label(new Rect(r.x + 12, r.y + 30, r.width - 24, r.height - 40), body, _bodyStyle);
        }

        private void DrawToast(string msg)
        {
            float w = 520, h = 44;
            Rect r = new Rect(12, Screen.height - 70, w, h);
            DrawRect(r, _panel2);
            DrawRect(new Rect(r.x, r.y, 4, r.height), _accent);
            GUI.Label(new Rect(r.x + 12, r.y + 12, r.width - 24, 22), msg, _bodyStyle);
        }

        private void DrawEnemyLine(string msg)
        {
            float w = 640, h = 46;
            float x = (Screen.width - w) * 0.5f;
            float y = 82;
            Rect r = new Rect(x, y, w, h);
            DrawRect(r, _panel2);
            DrawRect(new Rect(r.x, r.y + r.height - 3, r.width, 3), _accent);
            GUI.Label(new Rect(r.x + 14, r.y + 12, r.width - 28, 22), msg, _bodyStyle);
        }

        // -------------------- Subtitles --------------------
        private void DrawSubtitles()
        {
            var sm = SubtitleManager.Instance;
            if (sm == null) return;

            var lines = sm.GetLinesNewestFirst();
            if (lines == null || lines.Count == 0) return;

            float w = Mathf.Min(920f, Screen.width - 40f);
            float h = 54f + (lines.Count - 1) * 22f;

            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - (letterbox ? 150f : 110f) - h;

            Rect r = new Rect(x, y, w, h);
            DrawRect(r, _panel2);
            DrawRect(new Rect(r.x, r.y, 4, r.height), _accent);

            for (int i = 0; i < lines.Count; i++)
            {
                GUI.Label(new Rect(r.x + 14, r.y + 12 + i * 22, r.width - 28, 22), lines[i], _bodyStyle);
            }
            // Intro Help Overlay (draw only in OnGUI)
            if (introHelpEnabled && _showIntroHelp)
            {
                DrawIntroHelpOverlay();
            }


        }

        // -------------------- Phase / Objective --------------------
        private void DrawTopLeftPhase()
        {
            if (_episode == null || _episode.Current == null) return;

            var p = _episode.Current;
            string header = $"{_episode.episode.episodeName} / {p.title}";
            string body = p.description;

            DrawPanel(new Rect(12, 12, 520, 92), header, body, accent: true);

            if (p.phaseType == EpisodePhaseType.Intro)
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return) _episode.NextPhase();
            }
            else if (p.phaseType == EpisodePhaseType.Investigation)
            {
                var inv = InvestigationManager.Instance;
                if (inv != null && inv.CollectedCount >= inv.TargetCount)
                {
                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return) _episode.NextPhase();
                }
            }
            else if (p.phaseType == EpisodePhaseType.Outro)
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return) _episode.NextPhase();
            }
        }

        private void DrawObjectiveNavi()
        {
            if (_episode == null || _episode.Current == null) return;

            string title = "目的";
            string body = BuildObjectiveText(_episode.Current);

            float w = 420f;
            float h = 96f;
            float x = Screen.width - w - 12f;
            float y = Screen.height - h - 12f;

            DrawPanel(new Rect(x, y, w, h), title, body, accent: true);
        }

        private string BuildObjectiveText(EpisodePhase p)
        {
            switch (p.phaseType)
            {
                case EpisodePhaseType.Intro:
                    return "Enterで開始。\n調査→崩し→交渉で収束させる。";

                case EpisodePhaseType.Investigation:
                    {
                        var inv = InvestigationManager.Instance;
                        int c = inv != null ? inv.CollectedCount : 0;
                        int t = inv != null ? inv.TargetCount : p.targetEvidenceCount;
                        string s = $"証拠を集める（{c}/{t}）\n調査ポイント付近でE。";
                        if (inv != null && c >= t) s += "\n揃ったらEnterで収束作戦へ。";
                        return s;
                    }

                case EpisodePhaseType.Combat:
                    {
                        bool canNeg = false;
                        var enemy = FindObjectOfType<EnemyController>();
                        if (enemy != null)
                        {
                            var brk = enemy.GetComponent<Breakable>();
                            if (brk != null && brk.IsBroken) canNeg = true;
                        }
                        if (canNeg) return "敵が崩れている。\n近距離でF→交渉（1/2/3）で決着。";
                        return "敵をBreakさせる。\nE(Seal)で崩しを狙う。\n規約違反は不利になる。";
                    }

                case EpisodePhaseType.Outro:
                    return "後日談を確認。\nEnterで完了。";
            }
            return "";
        }

        // -------------------- Combat readability --------------------
        private void DrawEnemyGauges()
        {
            if (_episode == null || _episode.Current == null) return;
            if (_episode.Current.phaseType != EpisodePhaseType.Combat) return;

            var enemy = FindObjectOfType<EnemyController>();
            if (enemy == null) return;

            var hp = enemy.GetComponent<Damageable>();
            var brk = enemy.GetComponent<Breakable>();
            if (hp == null || brk == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 world = enemy.transform.position + Vector3.up * 2.0f;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.1f) return;

            float x = sp.x - 120f;
            float y = (Screen.height - sp.y) - 52f;

            Rect box = new Rect(x, y, 240f, 50f);
            DrawRect(box, _panel2);
            DrawRect(new Rect(box.x, box.y, 4, box.height), _accent);

            float hpN = (hp.maxHp > 0f) ? Mathf.Clamp01(hp.hp / hp.maxHp) : 0f;
            float brN = (brk.maxBreak > 0f) ? Mathf.Clamp01(brk.breakValue / brk.maxBreak) : 0f;

            DrawGauge(new Rect(box.x + 14, box.y + 14, 210, 10), hpN, "HP");
            string bLabel = brk.IsBroken ? "BREAK (BROKEN)" : "BREAK";
            DrawGauge(new Rect(box.x + 14, box.y + 32, 210, 10), brN, bLabel);
        }

        private void DrawGauge(Rect r, float n, string label)
        {
            DrawRect(r, new Color(1f, 1f, 1f, 0.08f));
            DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(n), r.height), new Color(_accent.r, _accent.g, _accent.b, 0.85f));
            GUI.Label(new Rect(r.x, r.y - 16, r.width, 16), $"{label} {n:P0}", _smallStyle);
        }

        private void DrawNegotiationPrompt()
        {
            if (_episode == null || _episode.Current == null) return;
            if (_episode.Current.phaseType != EpisodePhaseType.Combat) return;

            var enemy = FindObjectOfType<EnemyController>();
            if (enemy == null) return;

            var brk = enemy.GetComponent<Breakable>();
            bool canNormal = (brk != null && brk.IsBroken);

            var meta = CaseMetaManager.Instance;
            bool canEmergency = (meta != null && meta.HasArbitrationPass);
            if (!canNormal && !canEmergency) return;

            var pc = FindObjectOfType<PlayerController>();
            if (pc == null) return;

            var cd = FindObjectOfType<CombatDirector>();
            float range = (cd != null) ? cd.negotiationRange : 2.2f;

            float d = Vector3.Distance(pc.transform.position, enemy.transform.position);
            if (d > range) return;

            float w = 560f, h = 56f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - (letterbox ? 200f : 140f);

            Rect r = new Rect(x, y, w, h);
            DrawRect(r, _panel);
            DrawRect(new Rect(r.x, r.y, 4, r.height), _accent);

            GUI.Label(new Rect(r.x + 14, r.y + 10, r.width - 28, 22), "NEGOTIATION AVAILABLE", _smallStyle);
            string line = canNormal
                ? (canEmergency ? "F：交渉（Broken）  /  R：緊急介入（許可証）  /  1-4：選択" : "F：交渉を開始（1-3：選択）")
                : "R：緊急介入（許可証）  /  1-4：選択";
            GUI.Label(new Rect(r.x + 14, r.y + 28, r.width - 28, 22), line, _bodyStyle);
        }

        // -------------------- Rules / Warnings --------------------
        private void DrawRulesPanel()
        {
            if (RuleManager.Instance && RuleManager.Instance.activeRules.Count > 0)
            {
                float rx = Screen.width - 380f;
                float ry = 12f;

                Rect r = new Rect(rx, ry, 360f, 28f + RuleManager.Instance.activeRules.Count * 18f);
                DrawRect(r, _panel);
                DrawRect(new Rect(r.x, r.y, 4, r.height), _accent);

                GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 24, 18), "規約（Active Rules）", _smallStyle);

                float y = r.y + 28;
                foreach (var rule in RuleManager.Instance.activeRules)
                {
                    if (!rule) continue;
                    // ✅ 最初は「？？？」、証拠/違反で確定（RuleManager側で表示を切替）
                    string line = RuleManager.Instance.GetRulePanelLine(rule);
                    GUI.Label(new Rect(r.x + 12, y, r.width - 24, 18), $"・{line}", _bodyStyle);
                    y += 18;
                }
            }
        }

        private void DrawInfiltrationHUD()
        {
            // Step5: 潜入ミニゲームHUD（調査フェーズのみ）
            if (_episode == null || _episode.Current == null) return;
            if (_episode.Current.phaseType != EpisodePhaseType.Investigation) return;

            var inf = InfiltrationManager.Instance;
            if (inf == null) return;

            float rx = Screen.width - 380f;
            float rulesH = 0f;
            var rm = RuleManager.Instance;
            if (rm != null && rm.activeRules != null && rm.activeRules.Count > 0)
                rulesH = 28f + rm.activeRules.Count * 18f + 8f;

            float ry = 12f + rulesH + 8f;
            Rect r = new Rect(rx, ry, 360f, 86f);
            DrawRect(r, _panel);
            DrawRect(new Rect(r.x, r.y, 4, r.height), new Color(0.95f, 0.35f, 0.35f, 1f));

            GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 24, 18), "潜入（Security）", _smallStyle);

            var meta = CaseMetaManager.Instance;
            string line1 = $"ALERT: {(inf.alert01 * 100f):0}%";
            string line2 = inf.IsLockdownActive
                ? $"LOCKDOWN: {inf.LockdownRemaining:0.0}s（この間、Eで証拠回収不可）"
                : "カメラ視界を避けてEで証拠回収（見つかるとLOCKDOWN）";

            string sec = (meta != null)
                ? $"監視 x{meta.GetSecurityMultiplier():0.0}  / 期限 {meta.truceDebt}  歪み {meta.distortion}"
                : "監視 x1.0";

            string audit = inf.IsAuditActive
                ? $"AUDIT: ACTIVE {inf.AuditRemaining:0.0}s"
                : $"AUDIT in {inf.AuditNextIn:0.0}s";

            GUI.Label(new Rect(r.x + 12, r.y + 26, r.width - 24, 18), line1, _bodyStyle);
            GUI.Label(new Rect(r.x + 12, r.y + 44, r.width - 24, 18), line2, _smallStyle);
            GUI.Label(new Rect(r.x + 12, r.y + 62, r.width - 24, 18), $"{sec}  /  {audit}", _smallStyle);
        }


        private void DrawRuleWarnings()
        {
            if (_episode == null || _episode.Current == null) return;
            if (_episode.Current.phaseType != EpisodePhaseType.Combat) return;

            var rm = RuleManager.Instance;
            if (rm == null || rm.activeRules == null) return;

            float x = 12f;
            float y = 114f;

            for (int i = 0; i < rm.activeRules.Count; i++)
            {
                var r = rm.activeRules[i];
                if (r == null) continue;

                string rn = rm.GetPlayerFacingRuleName(r);
                bool known = rm.IsRevealed(r);

                if (r.ruleType == RuleType.GazeProhibition)
                {
                    float limit = rm.GetEffectiveGazeSecondsToViolate(r);
                    float remain = Mathf.Max(0f, limit - rm.GazeTimerSeconds);
                    if (rm.GazeTimerSeconds > 0.05f)
                    {
                        string body = known
                            ? $"[{rn}] 視線違反まで：{remain:0.0}s（ロックオン解除推奨）"
                            : $"[{rn}] 規約違反が近い：{remain:0.0}s";
                        DrawPanel(new Rect(x, y, 460, 54), "RULE WARNING", body, accent: true);
                        y += 60f;
                    }
                }
                else if (r.ruleType == RuleType.RepeatAttackProhibition)
                {
                    if (rm.RepeatWindowSeconds > 0.01f && rm.RepeatCount > 0)
                    {
                        int limitCount = rm.GetEffectiveRepeatCountToViolate(r);
                        string body = known
                            ? $"[{rn}] 同じ手：{rm.RepeatAttackType}  {rm.RepeatCount}/{limitCount}"
                            : $"[{rn}] 規約違反が近い：{rm.RepeatCount}/{limitCount}";
                        DrawPanel(new Rect(x, y, 460, 54), "RULE WARNING", body, accent: true);
                        y += 60f;
                    }
                }
            }
        }

        // -------------------- Negotiation UI --------------------
        private void DrawNegotiationPanel()
        {
            var nm = NegotiationManager.Instance;
            if (nm == null || !nm.IsOpen || nm.Current == null) return;

            // Step4：封印儀式（入力ミニゲーム）
            if (nm.IsSealRitualActive)
            {
                DrawSealRitualPanel(nm);
                return;
            }

            var def = nm.Current;

            float w = 760f, h = 392f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            DrawRect(new Rect(x, y, w, h), _panel);
            DrawRect(new Rect(x, y, 4, h), _accent);

            GUI.Label(new Rect(x + 16, y + 12, w - 32, 24), def.title, _titleStyle);
            GUI.Label(new Rect(x + 16, y + 44, w - 32, 44), def.prompt, _bodyStyle);

            var log = RunLogManager.Instance;
            int vc = (log != null) ? log.ViolationCount : 0;
            int hits = (log != null) ? log.PlayerHitCount : 0;
            float cost01 = (log != null) ? log.GetAdministrativeCost01() : 0f;
            float insight = (log != null) ? log.GetNegotiationInsightBonus() : 0f;

            // Stance
            string stanceName = nm.CurrentStance switch
            {
                NegotiationStance.Firm => "強硬（譲歩なし）",
                NegotiationStance.Balanced => "標準（小譲歩）",
                NegotiationStance.Concede => "譲歩（大）",
                _ => "標準"
            };

            float stanceCost = nm.GetAdminCostDelta(nm.CurrentStance);
            int stanceReduce = nm.GetStanceReduction(nm.CurrentStance);
            int insightReduce = Mathf.FloorToInt(insight / Mathf.Max(0.0001f, NegotiationManager.InsightPerGateReduction));

            float bmul = nm.GetBureaucracyCostMultiplier();
            GUI.Label(new Rect(x + 16, y + 90, w - 32, 20),
                $"行政コスト: {cost01:P0}（違反 {vc} / 被弾 {hits}）   学習: +{insight:P0}（条件緩和 -{insightReduce}）   監査補正 x{bmul:0.00}",
                _smallStyle);

            GUI.Label(new Rect(x + 16, y + 110, w - 32, 20),
                $"取引姿勢: {stanceName}（条件緩和 -{stanceReduce} / 追加コスト +{stanceCost:P0}）  ※Z/Xで変更",
                _smallStyle);

            float rowY = y + 142f;
            for (int i = 0; i < def.options.Length; i++)
            {
                var o = def.options[i];

                int required, have, total, stRed, inRed, finalReq;
                float adminDelta;
                bool can;
                nm.TryComputeGate(i, nm.CurrentStance, out required, out have, out total, out stRed, out inRed, out finalReq, out adminDelta, out can);

                string extra = (o.success == NegotiationOutcome.Seal && def.sealRitualEnabled) ? "  [儀式]" : "";
                string line = $"{i + 1}. {o.label}{extra}   成立条件: {have}/{finalReq}  （必要 {required} -譲歩{stRed} -学習{inRed}）   コスト +{adminDelta:P0}";
                GUI.Label(new Rect(x + 16, rowY, w - 32, 20), line, _bodyStyle);

                string evText = NegotiationManager.EvidenceListToText(o.evidenceBonusTags);
                string prog = (total > 0) ? $"  [所持 {have}/{total}]" : "";
                GUI.Label(new Rect(x + 36, rowY + 20, w - 52, 18), $"条件タグ: {evText}{prog}", _smallStyle);

                if (can)
                    GUI.Label(new Rect(x + w - 140, rowY + 2, 120, 18), "OK", _smallStyle);
                else
                    GUI.Label(new Rect(x + w - 140, rowY + 2, 120, 18), "NG", _smallStyle);

                rowY += 62f;
            }

            // Counter offer
            if (nm.HasCounterOffer && !string.IsNullOrEmpty(nm.CounterOfferText))
            {
                DrawRect(new Rect(x + 12, y + h - 96, w - 24, 66), _panel2);
                GUI.Label(new Rect(x + 22, y + h - 92, w - 44, 60), nm.CounterOfferText, _smallStyle);
            }

            string keySpan = def.options.Length >= 4 ? "1-4" : "1-3";
            GUI.Label(new Rect(x + 16, y + h - 26, w - 32, 22), $"{keySpan}：選択  Z/X：姿勢  Enter：対案受諾  Esc：閉じる", _smallStyle);

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Z) nm.CycleStance(-1);
                if (Event.current.keyCode == KeyCode.X) nm.CycleStance(+1);

                if (Event.current.keyCode == KeyCode.Alpha1 && def.options.Length >= 1) nm.Choose(0);
                if (Event.current.keyCode == KeyCode.Alpha2 && def.options.Length >= 2) nm.Choose(1);
                if (Event.current.keyCode == KeyCode.Alpha3 && def.options.Length >= 3) nm.Choose(2);
                if (Event.current.keyCode == KeyCode.Alpha4 && def.options.Length >= 4) nm.Choose(3);

                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                    nm.AcceptCounterOffer();

                if (Event.current.keyCode == KeyCode.Escape) nm.Close();
            }
        }

        private static string ArrowSymbol(KeyCode k)
        {
            return k switch
            {
                KeyCode.UpArrow => "↑",
                KeyCode.LeftArrow => "←",
                KeyCode.DownArrow => "↓",
                KeyCode.RightArrow => "→",
                _ => "?"
            };
        }

        // Step4：封印は“成立後に儀式入力”を要求し、勝ち方の違いを手触りで見せる
        private void DrawSealRitualPanel(NegotiationManager nm)
        {
            var def = nm.Current;

            float w = 720f, h = 280f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            DrawRect(new Rect(x, y, w, h), _panel);
            DrawRect(new Rect(x, y, 4, h), _accent);

            GUI.Label(new Rect(x + 16, y + 12, w - 32, 24), "封印儀式", _titleStyle);
            GUI.Label(new Rect(x + 16, y + 44, w - 32, 22), "↑←↓→ を順番に入力（ミス/時間切れで失敗）", _bodyStyle);

            int len = (nm.SealRitualSequence != null) ? nm.SealRitualSequence.Length : 0;
            int idx = nm.SealRitualIndex;

            // Sequence display
            float sx = x + 16f;
            float sy = y + 92f;
            float bw = 56f;
            for (int i = 0; i < len; i++)
            {
                var r = new Rect(sx + i * (bw + 8f), sy, bw, 56f);
                bool isNow = (i == idx);
                bool isDone = (i < idx);

                Color bg = isDone ? new Color(_accent.r, _accent.g, _accent.b, 0.25f)
                                  : isNow ? new Color(_accent.r, _accent.g, _accent.b, 0.45f)
                                          : _panel2;
                DrawRect(r, bg);
                DrawRect(new Rect(r.x, r.y, 2, r.height), _accent);

                string s = (nm.SealRitualSequence != null) ? ArrowSymbol(nm.SealRitualSequence[i]) : "?";
                var st = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 26 };
                GUI.Label(r, s, st);
            }

            // Time bar
            float frac = (nm.SealRitualStepTimeTotal <= 0.0001f) ? 0f : Mathf.Clamp01(nm.SealRitualStepTimeLeft / nm.SealRitualStepTimeTotal);
            DrawRect(new Rect(x + 16, y + 170, w - 32, 16), _panel2);
            DrawRect(new Rect(x + 16, y + 170, (w - 32) * frac, 16), _accent);
            GUI.Label(new Rect(x + 16, y + 192, w - 32, 18), $"進行: {Mathf.Clamp(idx, 0, len)}/{len}", _smallStyle);

            GUI.Label(new Rect(x + 16, y + h - 26, w - 32, 22), "矢印キー：入力   Esc：中断（失敗扱いだが学習が進む）", _smallStyle);

            if (Event.current.type == EventType.KeyDown)
            {
                var kc = Event.current.keyCode;
                if (kc == KeyCode.UpArrow || kc == KeyCode.LeftArrow || kc == KeyCode.DownArrow || kc == KeyCode.RightArrow)
                    nm.InputSealRitual(kc);

                if (kc == KeyCode.Escape)
                    nm.AbortSealRitual();
            }
        }

        // -------------------- RunLog --------------------
        private void DrawRunLog()
        {
            var log = RunLogManager.Instance;
            if (log == null) return;

            float x = 12f, y = Screen.height - 260f, w = 560f, h = 240f;
            DrawRect(new Rect(x, y, w, h), _panel2);
            DrawRect(new Rect(x, y, 4, h), _accent);

            GUI.Label(new Rect(x + 12, y + 10, w - 24, 18), "解析ログ（Prototype）", _smallStyle);

            float ty = y + 30f;
            GUI.Label(new Rect(x + 12, ty, w - 24, 18), $"被弾回数: {log.PlayerHitCount} / 被ダメージ: {log.PlayerDamageTaken:0}", _bodyStyle);
            ty += 18f;
            float cost = log.GetAdministrativeCost01();
            float ins = log.GetNegotiationInsightBonus();
            GUI.Label(new Rect(x + 12, ty, w - 24, 18), $"規約違反: {log.ViolationCount} / 行政コスト: {cost:P0} / 学習: +{ins:P0}", _bodyStyle);
            ty += 18f;

            int max = 6;
            for (int i = 0; i < log.Violations.Count && i < max; i++)
            {
                var v = log.Violations[i];
                string shownName = v.ruleName;
                string shownReason = v.reason;

                var rm = RuleManager.Instance;
                if (rm != null && rm.TryGetRuleByName(v.ruleName, out var def))
                {
                    shownName = rm.GetPlayerFacingRuleName(def);
                    shownReason = rm.GetPlayerFacingViolationReason(def, v.reason);
                }

                GUI.Label(new Rect(x + 12, ty, w - 24, 18), $"- [{v.time:0.0}s] {shownName} ({shownReason})", _smallStyle);
                ty += 18f;
            }
        }

        // -------------------- Title / Completed --------------------
        private void DrawTitleScreen(GameFlowController flow)
        {
            float w = 860f, h = 460f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            DrawRect(new Rect(x, y, w, h), _panel);
            DrawRect(new Rect(x, y, 4, h), _accent);

            GUI.Label(new Rect(x + 20, y + 20, w - 40, 34), flow.gameTitle, _titleStyle);
            GUI.Label(new Rect(x + 20, y + 54, w - 40, 24), flow.subtitle, _bodyStyle);
            GUI.Label(new Rect(x + 20, y + 84, w - 40, 44), flow.conceptLine, _bodyStyle);

            var meta = CaseMetaManager.Instance;
            string carry = (meta != null) ? meta.GetCarryoverText() : "（メタ未生成）";
            GUI.Label(new Rect(x + 20, y + 118, w - 40, 18), $"CARRYOVER : {carry}", _smallStyle);

            DrawRect(new Rect(x + 20, y + 140, w - 40, 214), _panel2);
            GUI.Label(new Rect(x + 34, y + 152, w - 68, 18), "CONTROLS", _smallStyle);
            GUI.Label(new Rect(x + 34, y + 178, w - 68, 18), "WASD：移動 / Space：ジャンプ", _bodyStyle);
            GUI.Label(new Rect(x + 34, y + 200, w - 68, 18), "LMB：軽攻撃 / RMB：重攻撃 / E：Seal（崩し強）", _bodyStyle);
            GUI.Label(new Rect(x + 34, y + 222, w - 68, 18), "Tab：ロックオン（規約に注意）", _bodyStyle);
            GUI.Label(new Rect(x + 34, y + 244, w - 68, 18), "敵がBroken中＆近距離でF：交渉 → 1/2/3で選択（Z/Xで譲歩） / 封印は儀式（矢印キー）", _bodyStyle);
            GUI.Label(new Rect(x + 34, y + 266, w - 68, 18), "調査：ポイント付近でE（交渉成立条件の証拠）", _bodyStyle);
            GUI.Label(new Rect(x + 34, y + 288, w - 68, 18), "Q（契約時）：影の遮蔽（監視のRayを遮る）", _bodyStyle);

            GUI.Label(new Rect(x + 20, y + h - 86, w - 40, 20), "Enter：START    F5：デモ自動再生    F7/F8：Take", _bodyStyle);
            GUI.Label(new Rect(x + 20, y + h - 60, w - 40, 18), "※難易度選択なし（固定）。規約と判断で“難しさ”が変わる。", _smallStyle);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                flow.StartGame();
        }

        private void DrawCompletedScreen(GameFlowController flow)
        {
            float w = 920f, h = 520f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            DrawRect(new Rect(x, y, w, h), _panel);
            DrawRect(new Rect(x, y, 4, h), _accent);

            GUI.Label(new Rect(x + 20, y + 18, w - 40, 26), "EPISODE COMPLETE", _titleStyle);
            GUI.Label(new Rect(x + 20, y + 52, w - 40, 22), $"結果：{OutcomeText(flow.LastOutcome)}", _bodyStyle);

            var meta = CaseMetaManager.Instance;
            string carry = (meta != null) ? meta.GetCarryoverText() : "（メタ未生成）";
            GUI.Label(new Rect(x + 20, y + 78, w - 40, 18), $"次回引き継ぎ：{carry}", _smallStyle);
            GUI.Label(new Rect(x + 20, y + 102, w - 40, 90), flow.nextHookLine, _bodyStyle);

            GUI.Label(new Rect(x + 20, y + h - 46, w - 40, 22), "R：やり直す    T：タイトルへ戻る", _bodyStyle);

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.R) flow.RestartEpisode();
                if (Event.current.keyCode == KeyCode.T) flow.BackToTitle();
            }
        }

        private static string MakeEnemyLine(string ruleName)
        {
            if (string.IsNullOrEmpty(ruleName)) return "……";
            if (ruleName.Contains("視線")) return "怪異『見たね。……見た。』";
            if (ruleName.Contains("同じ手")) return "怪異『学習した。次は通らない。』";
            return "怪異『違反。違反。違反。』";
        }

        private static string OutcomeText(NegotiationOutcome o)
        {
            return o switch
            {
                NegotiationOutcome.Truce => "停戦（期限付き）",
                NegotiationOutcome.Contract => "契約（協力）",
                NegotiationOutcome.Seal => "封印",
                NegotiationOutcome.Slay => "討伐",
                _ => "未確定"
            };
        }
        // -------------------- Intro Help Overlay (IMGUI) --------------------
        private void DrawIntroHelpOverlay()
        {
            // NOTE: Must be called only from OnGUI
            float pad = 12f;
            float w = 520f;
            float h = 118f;
            float x = pad;
            float y = Screen.height - h - pad - (letterbox ? 70f : 0f);

            DrawRect(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.72f));
            DrawRect(new Rect(x, y, 6f, h), _accent);

            string header = _introPinned ? "操作ヘルプ（固定）" : "操作ヘルプ";
            string body = GetIntroStepText(_introStep);

            GUI.Label(new Rect(x + 14f, y + 10f, w - 24f, 22f), header, _titleStyle);
            GUI.Label(new Rect(x + 14f, y + 34f, w - 24f, h - 56f), body, _bodyStyle);

            string footer = $"[{introAdvanceKey}] 次へ / [クリック] 次へ / [{introToggleKey}] 固定 / [{introDismissKey}] 閉じる";
            GUI.Label(new Rect(x + 14f, y + h - 22f, w - 24f, 18f), footer, _smallStyle);
        }

        private string GetIntroStepText(int step)
        {
            switch (step)
            {
                default:
                case 0:
                    return "移動：WASD / カメラ：マウス\n攻撃：左=Light / 右=Heavy / E=Seal\n目的：調査→規約→崩し→交渉";
                case 1:
                    return "ロックオン：Tab\n遮蔽物（柱）で視線を切る（視線規約の対策）\n監視カメラConeは避けるほど有利";
                case 2:
                    return "同じ攻撃の連打は危険（反復規約の対策）\nLight/Heavyを混ぜて戦うと有利\n違反ログを見て次の試行で改善";
                case 3:
                    return "敵を崩す（Break）と交渉が可能\n近距離で[F]：交渉開始／対案が出たら[Enter]\n討伐以外（停戦/契約/封印）で決着できる";
            }
        }

    }
}
