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
        }

        private void BuildStyles()
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _titleStyle.normal.textColor = _text;

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _bodyStyle.normal.textColor = _text;

            _smallStyle = new GUIStyle(GUI.skin.label)
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
            if (brk == null || !brk.IsBroken) return;

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
            GUI.Label(new Rect(r.x + 14, r.y + 28, r.width - 28, 22), "F：交渉を開始（1 / 2 / 3 で選択）", _bodyStyle);
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
                    GUI.Label(new Rect(r.x + 12, y, r.width - 24, 18), $"・{rule.displayName}", _bodyStyle);
                    y += 18;
                }
            }
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

                if (r.ruleType == RuleType.GazeProhibition)
                {
                    float remain = Mathf.Max(0f, r.gazeSecondsToViolate - rm.GazeTimerSeconds);
                    if (rm.GazeTimerSeconds > 0.05f)
                    {
                        DrawPanel(new Rect(x, y, 460, 54), "RULE WARNING", $"視線違反まで：{remain:0.0}s（ロックオン解除推奨）", accent: true);
                        y += 60f;
                    }
                }
                else if (r.ruleType == RuleType.RepeatAttackProhibition)
                {
                    if (rm.RepeatWindowSeconds > 0.01f && rm.RepeatCount > 0)
                    {
                        DrawPanel(new Rect(x, y, 460, 54), "RULE WARNING", $"同じ手：{rm.RepeatAttackType}  {rm.RepeatCount}/{r.repeatCountToViolate}", accent: true);
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

            var def = nm.Current;

            float w = 720f, h = 360f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            DrawRect(new Rect(x, y, w, h), _panel);
            DrawRect(new Rect(x, y, 4, h), _accent);

            GUI.Label(new Rect(x + 16, y + 12, w - 32, 24), def.title, _titleStyle);
            GUI.Label(new Rect(x + 16, y + 44, w - 32, 44), def.prompt, _bodyStyle);

            float p = (RunLogManager.Instance != null) ? RunLogManager.Instance.GetNegotiationPenalty() : 0f;
            int vc = (RunLogManager.Instance != null) ? RunLogManager.Instance.ViolationCount : 0;
            GUI.Label(new Rect(x + 16, y + 90, w - 32, 20), $"規約違反ペナルティ: -{p:P0}（違反 {vc}回）", _smallStyle);

            float rowY = y + 118f;
            for (int i = 0; i < def.options.Length; i++)
            {
                var o = def.options[i];

                float baseC, bonus, penalty, finalC;
                int have, total;
                nm.TryComputeChance(i, out baseC, out bonus, out penalty, out finalC, out have, out total);

                string line = $"{i + 1}. {o.label}   成功率: {finalC:P0}（基本 {baseC:P0} + 証拠 {bonus:P0} - 違反 {penalty:P0}）";
                GUI.Label(new Rect(x + 16, rowY, w - 32, 20), line, _bodyStyle);

                string evText = NegotiationManager.EvidenceListToText(o.evidenceBonusTags);
                string prog = (total > 0) ? $"  [{have}/{total}]" : "";
                GUI.Label(new Rect(x + 36, rowY + 20, w - 52, 18), $"有利証拠: {evText}{prog}", _smallStyle);

                rowY += 56f;
            }

            GUI.Label(new Rect(x + 16, y + h - 30, w - 32, 22), "1/2/3：選択  Esc：閉じる", _smallStyle);

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Alpha1) nm.Choose(0);
                if (Event.current.keyCode == KeyCode.Alpha2) nm.Choose(1);
                if (Event.current.keyCode == KeyCode.Alpha3) nm.Choose(2);
                if (Event.current.keyCode == KeyCode.Escape) nm.Close();
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
            GUI.Label(new Rect(x + 12, ty, w - 24, 18), $"規約違反: {log.ViolationCount} / 交渉ペナルティ: -{log.GetNegotiationPenalty():P0}", _bodyStyle);
            ty += 18f;

            int max = 6;
            for (int i = 0; i < log.Violations.Count && i < max; i++)
            {
                var v = log.Violations[i];
                GUI.Label(new Rect(x + 12, ty, w - 24, 18), $"- [{v.time:0.0}s] {v.ruleName} ({v.reason})", _smallStyle);
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

            DrawRect(new Rect(x + 20, y + 140, w - 40, 190), _panel2);
            GUI.Label(new Rect(x + 34, y + 152, w - 68, 18), "CONTROLS", _smallStyle);
            GUI.Label(new Rect(x + 34, y + 178, w - 68, 18), "WASD：移動 / Space：ジャンプ", _bodyStyle);
            GUI.Label(new Rect(x + 34, y + 200, w - 68, 18), "LMB：軽攻撃 / RMB：重攻撃 / E：Seal（崩し強）", _bodyStyle);
            GUI.Label(new Rect(x + 34, y + 222, w - 68, 18), "Tab：ロックオン（視線規約に注意）", _bodyStyle);
            GUI.Label(new Rect(x + 34, y + 244, w - 68, 18), "敵がBroken中＆近距離でF：交渉 → 1/2/3で選択", _bodyStyle);
            GUI.Label(new Rect(x + 34, y + 266, w - 68, 18), "調査：ポイント付近でE（交渉成功率ボーナス）", _bodyStyle);

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
    }
}
