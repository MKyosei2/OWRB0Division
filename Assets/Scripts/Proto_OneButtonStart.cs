using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Title画面で迷子を作らない：Enter一発でCase01開始。
    /// UI側に手を入れず、FlowStateを見て自動で有効化する。
    /// </summary>
    public sealed class Proto_OneButtonStart : MonoBehaviour
    {
        private static Proto_OneButtonStart _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("Proto_OneButtonStart");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Proto_OneButtonStart>();
        }

        private void Update()
        {
            var flow = GameFlowController.Instance;
            if (flow == null) return;

            // Title時のみ有効：Enter/Returnで開始
            if (flow.State == FlowState.Title)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    flow.StartGame();
                    EventBus.Instance?.Toast("Case Start");
                }
            }
        }

        private void OnGUI()
        {
            var flow = GameFlowController.Instance;
            if (flow == null) return;
            if (flow.State != FlowState.Title) return;

            // 右下に小さく操作ガイドを表示（P/Dデモ用）
            var rect = new Rect(Screen.width - 380, Screen.height - 60, 360, 40);
            GUI.Label(rect, "Enter：Case01開始（デモ用ワンボタン） / H：操作ヘルプ", GUI.skin.label);
        }
    }
}