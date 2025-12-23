using System.Collections;
using UnityEngine;

namespace OJikaProto
{
    public class Proto_DemoMacro : MonoBehaviour
    {
        [Header("Keys")]
        public KeyCode startKey = KeyCode.F5;
        public KeyCode toggleCaptureKey = KeyCode.F9;

        [Header("Timing")]
        [Tooltip("1.0=約3分 / 2.0=約1.5分")]
        public float demoSpeed = 1.0f;

        [Header("Demo")]
        public bool autoStartOnPlay = false;

        private Coroutine _co;

        private GameFlowController _flow;
        private EpisodeController _ep;
        private DebugHUD _hud;
        private Proto_CameraDirector _camDir;
        private Proto_AutoPilot _auto;

        private void Start()
        {
            RefreshRefs();
            if (autoStartOnPlay) StartDemo();
        }

        private void Update()
        {
            if (Input.GetKeyDown(startKey)) StartDemo();
            if (Input.GetKeyDown(toggleCaptureKey))
            {
                _hud = FindObjectOfType<DebugHUD>();
                if (_hud != null) _hud.ToggleCaptureMode();
                SubtitleManager.Instance?.Add("【CAPTURE】表示切替", 1.2f);
            }
        }

        public void StartDemo()
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(DemoCo());
        }

        private IEnumerator DemoCo()
        {
            RefreshRefs();

            // 放置デモ：自動操作ON
            _auto?.BeginForDemo();

            if (_flow != null && _flow.State != FlowState.Title)
                _flow.BackToTitle();

            yield return Wait(0.8f);

            if (_hud != null)
            {
                _hud.captureMode = true;
                _hud.letterbox = true;
                _hud.HideRunSummary();
            }

            Subtitle("【3分デモ】CASE01：規約×アクション×交渉", 2.2f);
            Subtitle("難易度選択なし。規約と判断が“難しさ”を作る。", 2.4f);

            CameraOrbitAroundPlayer(radius: 6.5f, height: 3.2f, degrees: 60f, fov: 55f, seconds: 3.5f);
            yield return Wait(3.6f);

            _flow?.StartGame();
            yield return Wait(0.8f);

            Subtitle("【調査】証拠を揃えるほど、交渉の成功率が上がる。", 2.6f);
            CameraShotToInvestigationPoint(isA: true, move: 1.0f, hold: 1.0f, fov: 52f);
            yield return Wait(2.3f);

            ForceCollectEvidence(EvidenceTag.CCTV_Loop);
            Subtitle("証拠①：監視映像のループ", 1.8f);
            yield return Wait(1.8f);

            CameraShotToInvestigationPoint(isA: false, move: 1.0f, hold: 1.0f, fov: 52f);
            yield return Wait(2.3f);

            ForceCollectEvidence(EvidenceTag.Clock_DeviceHint);
            Subtitle("証拠②：時計装置の痕跡", 1.8f);
            yield return Wait(1.6f);

            ForceCollectEvidence(EvidenceTag.StationStaff_Avoid);
            ForceCollectEvidence(EvidenceTag.TicketGate_MemoryLoss);
            Subtitle("※デモ用：必要証拠を揃えた（成功率が見える状態）", 2.0f);
            yield return Wait(1.8f);

            Subtitle("【収束作戦】規約に従って“崩し(BREAK)”を作り、交渉で決着。", 3.0f);
            _ep?.DebugJumpToCombat();
            yield return Wait(0.9f);

            CameraShotCombatWide(move: 1.0f, hold: 2.6f, fov: 58f);
            yield return Wait(3.0f);

            Subtitle("【規約】視線を合わせ続けるな / 同じ手を続けるな", 2.6f);
            yield return Wait(2.0f);

            Subtitle("（デモ）違反を1回発生させ、ペナルティの存在を見せる。", 2.4f);
            ForceRuleViolation("視線を合わせるな", "（デモ）強制違反");
            yield return Wait(2.2f);

            Subtitle("違反が増えるほど、交渉成功率が下がり、BREAK復帰が早くなる。", 3.0f);
            yield return Wait(2.8f);

            Subtitle("【崩し】Seal(E)でBREAKを作る。BREAK中だけ交渉が開ける。", 3.0f);
            CameraShotEnemyClose(move: 0.8f, hold: 1.6f, fov: 48f);
            yield return Wait(1.8f);

            // 以降はAutoPilotが戦闘→BREAK→交渉→決着まで持っていく
            CameraOrbitEnemy(radius: 4.5f, height: 2.2f, degrees: 120f, fov: 50f, seconds: 3.8f);
            yield return Wait(6.0f);

            Subtitle("【まとめ】規約→崩し→交渉。判断が物語と難しさを作る。", 3.2f);
            CameraPullBackEnding(move: 1.2f, hold: 3.5f, fov: 60f);
            yield return Wait(3.8f);

            Subtitle("【次回フック】駅の“終電”が、もう一度来る。", 2.4f);
            yield return Wait(2.4f);

            // 自動操作OFF（自由操作に戻す）
            _auto?.EndForDemo();

            // ✅ 提出向けサマリー表示
            RefreshRefs();
            if (_hud != null)
                _hud.ShowRunSummary(BuildSummaryText());

            Subtitle("【デモ終了】Escでサマリーを閉じる / F7,F8でTake / F5で再生", 3.0f);

            _co = null;
        }

        private void RefreshRefs()
        {
            if (_flow == null) _flow = FindObjectOfType<GameFlowController>();
            if (_ep == null) _ep = FindObjectOfType<EpisodeController>();
            if (_hud == null) _hud = FindObjectOfType<DebugHUD>();
            if (_camDir == null) _camDir = FindObjectOfType<Proto_CameraDirector>();
            if (_auto == null) _auto = FindObjectOfType<Proto_AutoPilot>();
        }

        private void Subtitle(string text, float seconds) => SubtitleManager.Instance?.Add(text, seconds);

        // objectで返す（WaitForSecondsRealtime対応）
        private object Wait(float seconds)
        {
            float s = seconds / Mathf.Max(0.01f, demoSpeed);
            return new WaitForSecondsRealtime(s);
        }

        private void ForceCollectEvidence(EvidenceTag tag) => InvestigationManager.Instance?.Collect(tag);

        private void ForceRuleViolation(string ruleName, string reason)
        {
            RunLogManager.Instance?.LogViolation(ruleName, reason);
            EventBus.Instance?.RuleViolated(ruleName, reason, 0.85f);
        }

        private string BuildSummaryText()
        {
            var log = RunLogManager.Instance;
            var flow = FindObjectOfType<GameFlowController>();

            int vio = (log != null) ? log.ViolationCount : 0;
            int hits = (log != null) ? log.PlayerHitCount : 0;
            float dmg = (log != null) ? log.PlayerDamageTaken : 0f;
            float pen = (log != null) ? log.GetNegotiationPenalty() : 0f;

            string outcome = (flow != null) ? flow.LastOutcome.ToString() : "Unknown";

            // 交渉の成功率（今の定義/証拠/違反から算出）
            string chances = "";
            var nm = NegotiationManager.Instance;
            if (nm != null && nm.Current != null)
            {
                for (int i = 0; i < nm.Current.options.Length; i++)
                {
                    float baseC, bonus, penalty, finalC;
                    int have, total;
                    nm.TryComputeChance(i, out baseC, out bonus, out penalty, out finalC, out have, out total);
                    chances += $"{i + 1}. {nm.Current.options[i].label}\n"
                             + $"   成功率 {finalC:P0}（基本{baseC:P0}+証拠{bonus:P0}-違反{penalty:P0}）\n";
                }
            }
            else
            {
                chances = "（交渉画面が開いていないため算出不可）\n";
            }

            return
                $"OUTCOME : {outcome}\n" +
                $"VIOLATION : {vio}   PENALTY : -{pen:P0}\n" +
                $"PLAYER : HIT {hits}   DMG {dmg:0}\n\n" +
                $"NEGOTIATION CHANCES\n{chances}\n" +
                $"NOTES\n・Take番号は右上表示 / F7,F8で変更\n・Escでこのサマリーを閉じる";
        }

        // camera presets
        private void CameraOrbitAroundPlayer(float radius, float height, float degrees, float fov, float seconds)
        {
            if (_camDir == null) return;
            var pc = FindObjectOfType<PlayerController>();
            if (pc == null) return;
            _camDir.PlayOrbit(pc.transform, radius, height, degrees, fov, seconds / Mathf.Max(0.01f, demoSpeed));
        }

        private void CameraShotToInvestigationPoint(bool isA, float move, float hold, float fov)
        {
            if (_camDir == null) return;

            Vector3 focusPos = isA ? new Vector3(-2f, 0.5f, -1f) : new Vector3(2f, 0.5f, -1f);
            var dummy = new GameObject(isA ? "_Focus_IPA" : "_Focus_IPB");
            dummy.transform.position = focusPos;

            _camDir.PlayShot(dummy.transform, new Vector3(0f, 2.2f, -3.2f), fov, move / demoSpeed, hold / demoSpeed);
            Destroy(dummy, 6f);
        }

        private void CameraShotCombatWide(float move, float hold, float fov)
        {
            if (_camDir == null) return;

            var enemy = FindObjectOfType<EnemyController>();
            var pc = FindObjectOfType<PlayerController>();
            Transform focus = (enemy != null) ? enemy.transform : (pc != null ? pc.transform : null);
            if (focus == null) return;

            _camDir.PlayShot(focus, new Vector3(0f, 4.2f, -7.0f), fov, move / demoSpeed, hold / demoSpeed);
        }

        private void CameraShotEnemyClose(float move, float hold, float fov)
        {
            if (_camDir == null) return;
            var enemy = FindObjectOfType<EnemyController>();
            if (enemy == null) return;

            _camDir.PlayShot(enemy.transform, new Vector3(1.2f, 1.6f, -2.2f), fov, move / demoSpeed, hold / demoSpeed);
        }

        private void CameraOrbitEnemy(float radius, float height, float degrees, float fov, float seconds)
        {
            if (_camDir == null) return;
            var enemy = FindObjectOfType<EnemyController>();
            if (enemy == null) return;

            _camDir.PlayOrbit(enemy.transform, radius, height, degrees, fov, seconds / Mathf.Max(0.01f, demoSpeed));
        }

        private void CameraPullBackEnding(float move, float hold, float fov)
        {
            if (_camDir == null) return;

            var pc = FindObjectOfType<PlayerController>();
            if (pc == null) return;

            _camDir.PlayShot(pc.transform, new Vector3(0f, 6.5f, -12.0f), fov, move / demoSpeed, hold / demoSpeed);
        }
    }
}
