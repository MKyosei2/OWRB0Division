using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// 規約→崩し→交渉→決着の“1枚図解”を、F1でいつでも表示できるようにする。
    /// 既存UIに依存せず、OnGUIで軽量に描画。
    /// </summary>
    public sealed class Proto_FlowDiagramOverlay : MonoBehaviour
    {
        private static Proto_FlowDiagramOverlay _instance;
        private bool _show;

        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _box;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("Proto_FlowDiagramOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Proto_FlowDiagramOverlay>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _show = !_show;
                EventBus.Instance?.Toast(_show ? "Flow Diagram ON" : "Flow Diagram OFF");
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true
            };
            _box = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft
            };
        }

        private void OnGUI()
        {
            if (!_show) return;
            EnsureStyles();

            float w = 520f;
            float h = 220f;
            var rect = new Rect(20, Screen.height - h - 20, w, h);

            GUI.Box(rect, GUIContent.none, _box);

            float x = rect.x + 14;
            float y = rect.y + 12;

            GUI.Label(new Rect(x, y, w - 28, 26), "戦闘フロー（規約→崩し→交渉→決着）", _title);
            y += 32;

            string txt =
                "1) 調査：証拠を拾い“規約”のヒントを確定\n" +
                "2) 規約：禁忌/儀式が戦闘ルールとして発動（守る/破るの選択）\n" +
                "3) 崩し：装置破壊・位置取り・規約対応でブレイク条件を満たす\n" +
                "4) 交渉：条件（証拠）＋譲歩（行政コスト）で成立させる\n" +
                "5) 決着：討伐/封印/停戦（＝“勝ち方”が複数） → 後日談/評価へ";

            GUI.Label(new Rect(x, y, w - 28, h - 60), txt, _body);

            // 閉じ方
            GUI.Label(new Rect(rect.x + w - 140, rect.y + h - 30, 120, 20), "F1：閉じる", _body);
        }
    }
}