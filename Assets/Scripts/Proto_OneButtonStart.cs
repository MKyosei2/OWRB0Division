// Auto-updated: 2026-01-09
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

        [Header("Options")]
        [Tooltip("PublicBuildでもワンボタン開始を有効にする（提出デモ用）。")]
        public bool forceEnableInPublicBuild = false;

        [Tooltip("ガイド表示を行う（OnGUI）。")]
        public bool showGuide = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;

            var go = new GameObject(nameof(Proto_OneButtonStart));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Proto_OneButtonStart>();
        }

        private bool IsAllowed()
        {
            return forceEnableInPublicBuild || ProtoBuildConfig.AllowDemoAssistUI;
        }

        private void Update()
        {
            if (!IsAllowed()) return;

            var flow = GameFlowController.Instance;
            if (flow == null) return;
            if (flow.State != FlowState.Title) return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                flow.StartGame(); // Start / resume run (Case01 prototype)
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                EventBus.Instance?.Toast("操作：WASD移動 / マウス視点 / LMB攻撃 / Space回避 / E調べる");
            }
        }

        private void OnGUI()
        {
            if (!IsAllowed()) return;
            if (!showGuide) return;

            var flow = GameFlowController.Instance;
            if (flow == null) return;
            if (flow.State != FlowState.Title) return;

            // 右下に小さく操作ガイドを表示（P/Dデモ用）
            var rect = new Rect(Screen.width - 420, Screen.height - 64, 400, 44);
            GUI.Label(rect, "Enter：Case01開始（デモ用ワンボタン） / H：操作ヘルプ", GUI.skin.label);
        }
    }
}
