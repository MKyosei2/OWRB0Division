using System;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Step Final: "次に何をすれば良いか" を 1行で提示する。
    /// - 交渉失敗や規約違反で迷子にならない
    /// - デモ/試遊での説明コストを下げる
    ///
    /// 既存コードを汚さないため、EventBus の Toast/Violation をフックして表示する。
    /// </summary>
    public class ActionHintOverlay : MonoBehaviour
    {
        private const float DefaultShowSeconds = 4.5f;

        private string _text;
        private float _until;

        private GUIStyle _style;
        private GUIStyle _bg;
        private bool _stylesReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // 既に存在するなら何もしない（ドメインリロード/シーン再読み込み対策）
            if (FindObjectOfType<ActionHintOverlay>() != null) return;
            var go = new GameObject("[Proto] ActionHintOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<ActionHintOverlay>();
        }

        private void OnEnable()
        {
            var bus = EventBus.Instance;
            if (bus != null)
            {
                bus.OnToast += OnToast;
                bus.OnRuleViolation += OnViolation;
                bus.OnEpisodeComplete += OnComplete;
            }
        }

        private void OnDisable()
        {
            var bus = EventBus.Instance;
            if (bus != null)
            {
                bus.OnToast -= OnToast;
                bus.OnRuleViolation -= OnViolation;
                bus.OnEpisodeComplete -= OnComplete;
            }
        }

        private void OnComplete(NegotiationOutcome _)
        {
            // クリア時は邪魔なので消す
            _until = 0f;
            _text = null;
        }

        private void OnViolation(RuleViolationSignal sig)
        {
            // "違反" は学習に直結させたい：原因 + 次の行動を一文に
            var name = string.IsNullOrEmpty(sig.ruleName) ? "規約" : sig.ruleName;
            var reason = string.IsNullOrEmpty(sig.reason) ? "違反" : sig.reason;

            // ルール名に応じた具体ヒント（最小）
            string hint;
            if (name.Contains("視線") || reason.Contains("視線") || reason.Contains("Lock") || reason.Contains("ロック"))
                hint = "遮蔽物/背面取りで視線を切る（ロックオン維持に注意）";
            else if (name.Contains("連打") || reason.Contains("連打") || reason.Contains("same") || reason.Contains("同じ"))
                hint = "同一攻撃の連続を避ける（軽/重/スキルを混ぜる）";
            else if (name.Contains("床") || reason.Contains("床") || reason.Contains("踏"))
                hint = "危険床は回避/ジャンプで渡る（安全地帯を確保）";
            else
                hint = "原因ログを確認→次の試行で1つだけ直す";

            Show($"違反：{name}（{reason}）→ 次：{hint}", DefaultShowSeconds);
        }

        private void OnToast(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;

            // 交渉関連の迷子を防ぐ
            // Proto_Negotiation で "Insufficient Evidence" を出しているケースに対応
            if (msg.Contains("Insufficient") || msg.Contains("Evidence") || msg.Contains("証拠") || msg.Contains("不足"))
            {
                Show("ヒント：調査ポイントで証拠タグを集める → 規約が確定すると交渉が通りやすい", DefaultShowSeconds);
                return;
            }

            if (msg.Contains("Negotiation") && msg.Contains("Cooldown"))
            {
                Show("ヒント：少し時間を置くか、規約対応で再度ブレイクを狙う", DefaultShowSeconds);
                return;
            }

            if (msg.Contains("Seal Failed") || msg.Contains("Ritual") || msg.Contains("儀式"))
            {
                Show("ヒント：封印は入力ミスで失敗する。落ち着いて矢印順を確認（失敗しても学習で前進）", DefaultShowSeconds);
                return;
            }

            // その他のToastは表示しない（既存Toastと二重になる）
        }

        private void Show(string text, float seconds)
        {
            _text = text;
            _until = Time.unscaledTime + Mathf.Max(0.25f, seconds);
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            // GUI.skin は OnGUI 内なら安全
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                richText = false
            };

            _bg = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft
            };

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_text)) return;
            if (Time.unscaledTime > _until) { _text = null; return; }

            EnsureStyles();

            float pad = 10f;
            float w = Mathf.Min(Screen.width - 40f, 980f);
            float h = 54f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - 22f;

            var rect = new Rect(x, y, w, h);
            GUI.Box(rect, GUIContent.none, _bg);

            var inner = new Rect(rect.x + pad, rect.y + 6f, rect.width - pad * 2f, rect.height - 12f);
            GUI.Label(inner, _text, _style);
        }
    }
}
