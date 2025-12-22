// Assets/Scripts/Proto_UI.cs
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

        private AudioSource _audio;
        private Texture2D _flatTex;

        private float _flashA;
        private string _enemyLine;
        private float _enemyLineT;

        private string _toast;
        private float _toastT;

        private EpisodeController _episode;
        private GameFlowController _flow;

        // Time distortion runtime
        private float _baseFixedDelta;
        private float _slowT;
        private bool _recovering;

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

            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnToast += (msg) => { _toast = msg; _toastT = 2f; };
                EventBus.Instance.OnRuleViolation += OnRuleViolation;
            }
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
            if (_toastT > 0f) _toastT -= Time.deltaTime;

            if (_flashA > 0f)
                _flashA = Mathf.MoveTowards(_flashA, 0f, flashFadeSpeed * Time.deltaTime);

            if (_enemyLineT > 0f)
                _enemyLineT -= Time.deltaTime;

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

                if (_slowT <= 0f)
                    _recovering = true;

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
            if (_flashA > 0.001f)
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 0f, 0f, _flashA);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _flatTex);
                GUI.color = prev;
            }

            GUI.skin.label.fontSize = 14;

            if (_flow != null)
            {
                if (_flow.State == FlowState.Title)
                {
                    DrawTitleScreen(_flow);
                    return;
                }
                if (_flow.State == FlowState.Completed)
                {
                    DrawCompletedScreen(_flow); // ✅ リザルト強化
                    return;
                }
            }

            float y = 12f;
            if (_episode && _episode.Current != null)
            {
                var p = _episode.Current;
                GUI.Label(new Rect(12, y, 1200, 22), $"{_episode.episode.episodeName} / {p.title}");
                y += 20;
                GUI.Label(new Rect(12, y, 1200, 44), p.description);
                y += 44;

                if (p.phaseType == EpisodePhaseType.Intro)
                {
                    GUI.Label(new Rect(12, y, 1200, 22), "Enter：次へ");
                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                        _episode.NextPhase();
                }
                else if (p.phaseType == EpisodePhaseType.Investigation)
                {
                    var inv = InvestigationManager.Instance;
                    GUI.Label(new Rect(12, y, 1200, 22), $"証拠 {inv.CollectedCount}/{inv.TargetCount}  （近づいてEで取得）");
                    y += 22;
                    if (inv.CollectedCount >= inv.TargetCount)
                    {
                        GUI.Label(new Rect(12, y, 1200, 22), "Enter：収束作戦へ");
                        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                            _episode.NextPhase();
                    }
                }
                else if (p.phaseType == EpisodePhaseType.Combat)
                {
                    GUI.Label(new Rect(12, y, 1200, 22), "LMB=Light / RMB=Heavy / E=Seal / Tab=LockOn / Broken中に近距離でF=交渉");
                }
                else if (p.phaseType == EpisodePhaseType.Outro)
                {
                    DrawOutroPanel(_episode);
                }
            }

            if (_toastT > 0f && !string.IsNullOrEmpty(_toast))
                GUI.Box(new Rect(12, Screen.height - 70, 520, 42), _toast);

            if (_enemyLineT > 0f && !string.IsNullOrEmpty(_enemyLine))
            {
                float w = 620f, h = 44f;
                float x = (Screen.width - w) * 0.5f;
                float y2 = 22f;

                GUI.Box(new Rect(x, y2, w, h), "");
                GUI.Label(new Rect(x + 14f, y2 + 12f, w - 28f, 20f), _enemyLine);
            }

            DrawRulesPanel();
            DrawNegotiationPanel();
            DrawRunLog();

            if (showObjectiveNavi)
                DrawObjectiveNavi();
        }

        // ------------------------
        // Title / Completed
        // ------------------------
        private void DrawTitleScreen(GameFlowController flow)
        {
            float w = 760f, h = 420f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), "");
            GUI.skin.label.fontSize = 26;
            GUI.Label(new Rect(x + 20, y + 24, w - 40, 34), flow.gameTitle);

            GUI.skin.label.fontSize = 16;
            GUI.Label(new Rect(x + 20, y + 70, w - 40, 26), flow.subtitle);

            GUI.skin.label.fontSize = 14;
            GUI.Label(new Rect(x + 20, y + 108, w - 40, 44), flow.conceptLine);

            GUI.Box(new Rect(x + 20, y + 160, w - 40, 170), "CONTROLS");
            GUI.Label(new Rect(x + 36, y + 190, w - 72, 22), "WASD：移動 / Space：ジャンプ");
            GUI.Label(new Rect(x + 36, y + 212, w - 72, 22), "LMB：軽攻撃 / RMB：重攻撃 / E：Seal（崩し強）");
            GUI.Label(new Rect(x + 36, y + 234, w - 72, 22), "Tab：ロックオン（視線規約に注意）");
            GUI.Label(new Rect(x + 36, y + 256, w - 72, 22), "敵がBroken中＆近距離でF：交渉 → 1/2/3で選択");
            GUI.Label(new Rect(x + 36, y + 278, w - 72, 22), "調査：ポイント付近でE（交渉成功率ボーナス）");

            GUI.skin.label.fontSize = 15;
            GUI.Label(new Rect(x + 20, y + h - 64, w - 40, 24), "Enter：START");
            GUI.skin.label.fontSize = 14;
            GUI.Label(new Rect(x + 20, y + h - 40, w - 40, 22), "※難易度選択なし（固定）。規約と判断で“難しさ”が変わる。");

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                flow.StartGame();
        }

        private void DrawCompletedScreen(GameFlowController flow)
        {
            var log = RunLogManager.Instance;
            var inv = InvestigationManager.Instance;

            // score
            int score;
            string rank;
            string tip;
            ComputeCaseScore(out score, out rank, out tip);

            float w = 920f, h = 540f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), "EPISODE COMPLETE");

            // Header
            GUI.skin.label.fontSize = 16;
            GUI.Label(new Rect(x + 20, y + 44, w - 40, 24), $"結果：{OutcomeText(flow.LastOutcome)}");

            GUI.skin.label.fontSize = 18;
            GUI.Label(new Rect(x + 20, y + 74, w - 40, 24), $"CASE SCORE：{score}  / 100    Rank：{rank}");

            // Outro 3 lines
            string l1 = "", l2 = "", l3 = "";
            if (_episode != null) _episode.TryGetOutroText(flow.LastOutcome, out l1, out l2, out l3);
            GUI.skin.label.fontSize = 14;
            GUI.Label(new Rect(x + 20, y + 108, w - 40, 20), l1);
            GUI.Label(new Rect(x + 20, y + 128, w - 40, 20), l2);
            GUI.Label(new Rect(x + 20, y + 148, w - 40, 20), l3);

            // Metrics box
            float bx = x + 20, by = y + 178, bw = w - 40, bh = 190;
            GUI.Box(new Rect(bx, by, bw, bh), "RESULT METRICS");

            float lineY = by + 28;

            int violationCount = (log != null) ? log.ViolationCount : 0;
            int hitCount = (log != null) ? log.PlayerHitCount : 0;
            float dmgTaken = (log != null) ? log.PlayerDamageTaken : 0f;
            float penalty = (log != null) ? log.GetNegotiationPenalty() : 0f;
            float breakMul = (log != null) ? log.GetBreakRecoverMultiplier() : 1f;

            int evC = (inv != null) ? inv.CollectedCount : 0;
            int evT = (inv != null) ? inv.TargetCount : 0;

            string runTimeText = "-";
            if (log != null) runTimeText = $"{(Time.time - log.RunStartTime):0.0}s";

            GUI.Label(new Rect(bx + 12, lineY, bw - 24, 20), $"Time: {runTimeText}    Evidence: {evC}/{evT}");
            lineY += 22;
            GUI.Label(new Rect(bx + 12, lineY, bw - 24, 20), $"Violations: {violationCount}    Hits: {hitCount}    Damage Taken: {dmgTaken:0}");
            lineY += 22;
            GUI.Label(new Rect(bx + 12, lineY, bw - 24, 20), $"Negotiation Penalty: -{penalty:P0}    Break Recover: x{breakMul:0.00}（崩し猶予が短い）");
            lineY += 26;

            // Negotiation detail (last)
            string negoLine = "Negotiation: -";
            if (log != null && log.Negotiations.Count > 0)
            {
                var n = log.Negotiations[log.Negotiations.Count - 1];
                negoLine = $"Negotiation: \"{n.option}\"    Chance: {n.chance:P0}    Success: {n.success}";
            }
            GUI.Label(new Rect(bx + 12, lineY, bw - 24, 20), negoLine);

            // Next hook
            GUI.Box(new Rect(x + 20, y + 380, w - 40, 110), "NEXT HOOK");
            GUI.Label(new Rect(x + 34, y + 408, w - 68, 78), flow.nextHookLine);

            // Auto tip
            GUI.Box(new Rect(x + 20, y + 498, w - 40, 30), "NEXT IMPROVEMENT");
            GUI.Label(new Rect(x + 34, y + 505, w - 68, 20), tip);

            // Controls
            GUI.Label(new Rect(x + 20, y + h - 36, w - 40, 22), "R：同じ1話をやり直す（検証）    T：タイトルへ戻る");

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.R) flow.RestartEpisode();
                if (Event.current.keyCode == KeyCode.T) flow.BackToTitle();
            }
        }

        private void ComputeCaseScore(out int score, out string rank, out string tip)
        {
            var log = RunLogManager.Instance;

            // Base
            float s = 100f;

            int vio = (log != null) ? log.ViolationCount : 0;
            int hits = (log != null) ? log.PlayerHitCount : 0;
            float dmg = (log != null) ? log.PlayerDamageTaken : 0f;

            // Penalties tuned for “プロトで気持ちよく差が出る”重み
            s -= vio * 12f;          // 規約違反は重い
            s -= hits * 3.5f;        // 被弾は中
            s -= dmg * 0.12f;        // 被ダメージは微

            // Clamp
            s = Mathf.Clamp(s, 0f, 100f);
            score = Mathf.RoundToInt(s);

            if (score >= 90) rank = "S";
            else if (score >= 75) rank = "A";
            else if (score >= 60) rank = "B";
            else if (score >= 45) rank = "C";
            else rank = "D";

            // Tip
            if (vio >= 3) tip = "規約違反が多い：ロックオン維持と“同じ手”の連打を減らすと、交渉も崩し猶予も改善。";
            else if (hits >= 8) tip = "被弾が多い：Seal（E）で崩しを作って“短期決着”へ。距離管理を優先。";
            else if (dmg >= 120) tip = "被ダメージが大きい：重攻撃連打より“様子見→崩し→交渉”の順で安定。";
            else tip = "次は“証拠ボーナスを最大化”して交渉成功率を押し上げ、狙った結末を再現してみて。";
        }

        // ------------------------
        // In-game panels
        // ------------------------
        private void DrawObjectiveNavi()
        {
            if (_episode == null || _episode.Current == null) return;

            string title = "目的";
            string body = BuildObjectiveText(_episode.Current);

            float w = 420f;
            float h = 92f;
            float x = Screen.width - w - 12f;
            float y = Screen.height - h - 12f;

            GUI.Box(new Rect(x, y, w, h), title);
            GUI.Label(new Rect(x + 12f, y + 26f, w - 24f, h - 32f), body);
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

                        if (canNeg)
                            return "敵が崩れている。\n近距離でF→交渉（1/2/3）で決着。";

                        return "敵をBreakさせる。\nE(Seal)で崩しを狙う。\n規約違反は不利になる。";
                    }

                case EpisodePhaseType.Outro:
                    return "後日談を確認。\nEnterで完了。";

                default:
                    return "";
            }
        }

        private static string MakeEnemyLine(string ruleName)
        {
            if (string.IsNullOrEmpty(ruleName)) return "……";
            if (ruleName.Contains("視線")) return "怪異『見たね。……見た。』";
            if (ruleName.Contains("同じ手")) return "怪異『学習した。次は通らない。』";
            return "怪異『違反。違反。違反。』";
        }

        private void DrawOutroPanel(EpisodeController ep)
        {
            float w = 720f, h = 260f;
            float x = (Screen.width - w) * 0.5f;
            float y = 120f;

            GUI.Box(new Rect(x, y, w, h), "後日談");

            string l1, l2, l3;
            ep.TryGetOutroText(ep.LastOutcome, out l1, out l2, out l3);

            GUI.Label(new Rect(x + 18, y + 38, w - 36, 26), $"結果：{OutcomeText(ep.LastOutcome)}");
            GUI.Label(new Rect(x + 18, y + 78, w - 36, 26), l1);
            GUI.Label(new Rect(x + 18, y + 108, w - 36, 26), l2);
            GUI.Label(new Rect(x + 18, y + 138, w - 36, 26), l3);

            GUI.Label(new Rect(x + 18, y + h - 40, w - 36, 26), "Enter：完了");

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                ep.NextPhase();
        }

        private void DrawRulesPanel()
        {
            if (RuleManager.Instance && RuleManager.Instance.activeRules.Count > 0)
            {
                float rx = Screen.width - 380f;
                float ry = 12f;
                GUI.Box(new Rect(rx, ry, 360f, 24f + RuleManager.Instance.activeRules.Count * 20f), "規約（Active Rules）");
                ry += 24f;
                foreach (var r in RuleManager.Instance.activeRules)
                {
                    if (r == null) continue;
                    GUI.Label(new Rect(rx + 10f, ry, 340f, 20f), $"・{r.displayName}");
                    ry += 18f;
                }
            }
        }

        private void DrawNegotiationPanel()
        {
            var nm = NegotiationManager.Instance;
            if (nm == null || !nm.IsOpen || nm.Current == null) return;

            var def = nm.Current;

            float w = 680f, h = 350f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), def.title);
            GUI.Label(new Rect(x + 12f, y + 34f, w - 24f, 46f), def.prompt);

            float p = (RunLogManager.Instance != null) ? RunLogManager.Instance.GetNegotiationPenalty() : 0f;
            int vc = (RunLogManager.Instance != null) ? RunLogManager.Instance.ViolationCount : 0;
            GUI.Label(new Rect(x + 12f, y + 68f, w - 24f, 20f), $"規約違反ペナルティ: -{p:P0}（違反 {vc}回）");

            float rowY = y + 96f;
            for (int i = 0; i < def.options.Length; i++)
            {
                var o = def.options[i];

                float baseC, bonus, penalty, finalC;
                int have, total;
                nm.TryComputeChance(i, out baseC, out bonus, out penalty, out finalC, out have, out total);

                GUI.Label(new Rect(x + 12f, rowY, w - 24f, 20f),
                    $"{i + 1}. {o.label}   成功率: {finalC:P0} （基本 {baseC:P0} + 証拠 {bonus:P0} - 違反 {penalty:P0}）");

                string evText = NegotiationManager.EvidenceListToText(o.evidenceBonusTags);
                string prog = (total > 0) ? $"  [{have}/{total}]" : "";
                GUI.Label(new Rect(x + 32f, rowY + 18f, w - 44f, 20f), $"有利証拠: {evText}{prog}");

                rowY += 54f;
            }

            GUI.Label(new Rect(x + 12f, y + h - 28f, w - 24f, 24f), "1/2/3：選択  Esc：閉じる");

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Alpha1) nm.Choose(0);
                if (Event.current.keyCode == KeyCode.Alpha2) nm.Choose(1);
                if (Event.current.keyCode == KeyCode.Alpha3) nm.Choose(2);
                if (Event.current.keyCode == KeyCode.Escape) nm.Close();
            }
        }

        private void DrawRunLog()
        {
            var log = RunLogManager.Instance;
            if (log == null) return;

            float x = 12f, y = Screen.height - 250f, w = 520f, h = 230f;
            GUI.Box(new Rect(x, y, w, h), "解析ログ（Prototype）");

            float ty = y + 24f;
            GUI.Label(new Rect(x + 10f, ty, w - 20f, 18f), $"被弾回数: {log.PlayerHitCount} / 被ダメージ(概算): {log.PlayerDamageTaken:0}");
            ty += 18f;
            GUI.Label(new Rect(x + 10f, ty, w - 20f, 18f), $"規約違反: {log.ViolationCount}  / 交渉ペナルティ: -{log.GetNegotiationPenalty():P0}");
            ty += 18f;

            int max = 6;
            for (int i = 0; i < log.Violations.Count && i < max; i++)
            {
                var v = log.Violations[i];
                GUI.Label(new Rect(x + 10f, ty, w - 20f, 18f), $"- [{v.time:0.0}s] {v.ruleName} ({v.reason})");
                ty += 18f;
            }

            if (log.Negotiations.Count > 0)
            {
                ty += 6f;
                var n = log.Negotiations[log.Negotiations.Count - 1];
                GUI.Label(new Rect(x + 10f, ty, w - 20f, 18f), $"交渉: {n.option}  chance:{n.chance:P0}  success:{n.success}");
            }
        }

        private static string OutcomeText(NegotiationOutcome o)
        {
            return o switch
            {
                NegotiationOutcome.Truce => "停戦成立（期限付き）",
                NegotiationOutcome.Contract => "契約成立（協力）",
                NegotiationOutcome.Seal => "封印完了",
                NegotiationOutcome.Slay => "討伐",
                _ => "未確定"
            };
        }
    }
}
