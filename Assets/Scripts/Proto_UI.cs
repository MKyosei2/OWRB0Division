// Assets/Scripts/Proto_UI.cs
using UnityEngine;

namespace OJikaProto
{
    public class DebugHUD : MonoBehaviour
    {
        [Header("Feedback (Optional)")]
        public AudioClip ruleViolationSfx; // 任意：Inspectorで入れる
        public float flashFadeSpeed = 2.4f;

        private AudioSource _audio;
        private Texture2D _flatTex;

        private float _flashA;           // 0-1
        private string _enemyLine;
        private float _enemyLineT;

        private string _toast;
        private float _toastT;

        private EpisodeController _episode;

        private void Awake()
        {
            CoreEnsure.EnsureAll();
            _episode = FindObjectOfType<EpisodeController>();

            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;

            _flatTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _flatTex.SetPixel(0, 0, Color.white);
            _flatTex.Apply();

            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnToast += (msg) => { _toast = msg; _toastT = 2f; };
                EventBus.Instance.OnRuleViolation += OnRuleViolation; // ✅
            }
        }

        private void OnDestroy()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnRuleViolation -= OnRuleViolation;
        }

        private void OnRuleViolation(RuleViolationSignal sig)
        {
            _flashA = Mathf.Max(_flashA, Mathf.Clamp01(sig.intensity) * 0.9f);
            _enemyLine = MakeEnemyLine(sig.ruleName);
            _enemyLineT = 2.0f;

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
        }

        private void OnGUI()
        {
            // ✅ 画面フラッシュ（赤）
            if (_flashA > 0.001f)
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 0f, 0f, _flashA);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _flatTex);
                GUI.color = prev;
            }

            GUI.skin.label.fontSize = 14;

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

            // Toast
            if (_toastT > 0f && !string.IsNullOrEmpty(_toast))
                GUI.Box(new Rect(12, Screen.height - 70, 520, 42), _toast);

            // ✅ 敵の一言（違反時）
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
        }

        private static string MakeEnemyLine(string ruleName)
        {
            if (string.IsNullOrEmpty(ruleName)) return "……";

            // ルール名に合わせて台詞を変える（アトラス風：短く、刺す）
            if (ruleName.Contains("視線"))
                return "怪異『見たね。……見た。』";
            if (ruleName.Contains("同じ手"))
                return "怪異『学習した。次は通らない。』";
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

            GUI.Label(new Rect(x + 18, y + h - 40, w - 36, 26), "Enter：終了（プロト）");

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

            float w = 640f, h = 320f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), def.title);
            GUI.Label(new Rect(x + 12f, y + 34f, w - 24f, 46f), def.prompt);

            float rowY = y + 86f;
            for (int i = 0; i < def.options.Length; i++)
            {
                var o = def.options[i];

                float baseC, bonus, finalC;
                int have, total;
                nm.TryComputeChance(i, out baseC, out bonus, out finalC, out have, out total);

                GUI.Label(new Rect(x + 12f, rowY, w - 24f, 20f),
                    $"{i + 1}. {o.label}   成功率: {finalC:P0} （基本 {baseC:P0} + ボーナス {bonus:P0}）");

                string evText = NegotiationManager.EvidenceListToText(o.evidenceBonusTags);
                string prog = (total > 0) ? $"  [{have}/{total}]" : "";
                GUI.Label(new Rect(x + 32f, rowY + 18f, w - 44f, 20f), $"有利証拠: {evText}{prog}");

                rowY += 50f;
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
            GUI.Label(new Rect(x + 10f, ty, w - 20f, 18f), $"規約違反: {log.Violations.Count}");
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
