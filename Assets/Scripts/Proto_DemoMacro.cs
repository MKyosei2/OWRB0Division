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
        [Tooltip("1.0=標準 / 2.0=倍速")]
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
            
            if (ProtoBuildConfig.ShouldSuppressDebugInRuntime()) return;
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

            // タイトルへ戻す（存在する場合）
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

            Subtitle("【調査】証拠を揃えるほど、交渉の“成立条件”が満たされる。", 2.6f);
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
            Subtitle("※デモ用：必要証拠を揃えた（成立条件が満たされる）", 2.0f);
            yield return Wait(1.8f);

            Subtitle("【収束作戦】規約に従って“崩し(BREAK)”を作り、交渉で決着。", 3.0f);
            _ep?.DebugJumpToCombat();
            yield return Wait(0.9f);

            CameraShotCombatWide(move: 1.0f, hold: 2.6f, fov: 58f);
            yield return Wait(3.0f);

            Subtitle("【規約】視線を合わせ続けるな / 同じ手を続けるな", 2.6f);
            yield return Wait(2.0f);

            Subtitle("（デモ）違反を1回発生させ、行政コストとして記録されることを見せる。", 2.4f);
            ForceRuleViolation("視線を合わせるな", "（デモ）強制違反");
            yield return Wait(2.2f);

            Subtitle("違反や交渉失敗は“学習”になり、次の交渉条件が緩和される（代償は行政コスト）。", 3.0f);
            yield return Wait(2.8f);

            Subtitle("【崩し】Seal(E)でBREAKを作る。BREAK中だけ交渉が開ける。", 3.0f);
            CameraShotEnemyClose(move: 0.8f, hold: 1.6f, fov: 48f);
            yield return Wait(1.8f);

            // 以降はAutoPilotが戦闘→BREAK→交渉→決着まで持っていく想定
            CameraOrbitEnemy(radius: 4.5f, height: 2.2f, degrees: 120f, fov: 50f, seconds: 3.8f);
            yield return Wait(6.0f);

            Subtitle("【まとめ】規約→崩し→交渉。判断が物語と難しさを作る。", 3.2f);
            CameraPullBackEnding(move: 1.2f, hold: 3.5f, fov: 60f);
            yield return Wait(3.8f);

            Subtitle("【次回フック】駅の“終電”が、もう一度来る。", 2.4f);
            yield return Wait(2.4f);

            // 自動操作OFF（自由操作に戻す）
            _auto?.EndForDemo();

            // 提出向けサマリー表示
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

        private WaitForSecondsRealtime Wait(float seconds)
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
            float cost = (log != null) ? log.GetAdministrativeCost01() : 0f;
            float ins = (log != null) ? log.GetNegotiationInsightBonus() : 0f;
            string outcome = (flow != null) ? flow.LastOutcome.ToString() : "Unknown";
            string carry = (CaseMetaManager.Instance != null) ? CaseMetaManager.Instance.GetCarryoverText() : "（メタ未生成）";

            string chances = "";
            var nm = NegotiationManager.Instance;
            if (nm != null && nm.Current != null && nm.Current.options != null)
            {
                for (int i = 0; i < nm.Current.options.Length; i++)
                {
                    float baseC, bonus, penalty, finalC;
                    int have, total;
                    nm.TryComputeChance(i, out baseC, out bonus, out penalty, out finalC, out have, out total);
                    chances += $"{i + 1}. {nm.Current.options[i].label}\n"
                             + $"   成立度 {finalC:P0}（証拠/条件の充足度）\n";
                }
            }
            else
            {
                chances = "（交渉画面が開いていないため算出不可）\n";
            }

            return
                $"OUTCOME : {outcome}\n" +
                $"CARRYOVER : {carry}\n" +
                $"VIOLATION : {vio}   COST : {cost:P0}   LEARN : +{ins:P0}\n" +
                $"PLAYER : HIT {hits}   DMG {dmg:0}\n\n" +
                $"NEGOTIATION GATES\n{chances}\n" +
                $"NOTES\n・Take番号は右上表示 / F7,F8で変更\n・Escでこのサマリーを閉じる";
        }

        // =========================================================
        // Camera helpers (match existing Proto_CameraDirector API)
        //   PlayShot(Transform target, Vector3 offset, float moveSeconds, float holdSeconds, float fov)
        //   PlayOrbit(Transform target, float radius, float height, float degrees, float fov, float seconds)
        // =========================================================

        private void CameraOrbitAroundPlayer(float radius, float height, float degrees, float fov, float seconds)
        {
            if (_camDir == null) return;
            var pc = FindObjectOfType<PlayerController>();
            if (pc == null) return;

            _camDir.PlayOrbit(
                pc.transform,
                radius,
                height,
                degrees,
                fov,
                seconds / Mathf.Max(0.01f, demoSpeed)
            );
        }

        private void CameraShotToInvestigationPoint(bool isA, float move, float hold, float fov)
        {
            if (_camDir == null) return;

            var all = FindObjectsOfType<InvestigationPoint>();
            if (all == null || all.Length == 0) return;

            InvestigationPoint target = all[0];
            foreach (var p in all)
            {
                if (isA && p.name.Contains("_A")) { target = p; break; }
                if (!isA && p.name.Contains("_B")) { target = p; break; }
            }

            // 少し斜め後方から見る
            Vector3 offset = new Vector3(2.2f, 1.6f, -2.6f);

            _camDir.PlayShot(
                target.transform,
                offset,
                move / Mathf.Max(0.01f, demoSpeed),
                hold / Mathf.Max(0.01f, demoSpeed),
                fov
            );
        }

        private void CameraShotCombatWide(float move, float hold, float fov)
        {
            if (_camDir == null) return;
            var enemy = FindObjectOfType<EnemyController>();
            if (enemy == null) return;

            // 戦闘エリアを広めに押さえる
            Vector3 offset = new Vector3(0.0f, 3.0f, -6.0f);

            _camDir.PlayShot(
                enemy.transform,
                offset,
                move / Mathf.Max(0.01f, demoSpeed),
                hold / Mathf.Max(0.01f, demoSpeed),
                fov
            );
        }

        private void CameraShotEnemyClose(float move, float hold, float fov)
        {
            if (_camDir == null) return;
            var enemy = FindObjectOfType<EnemyController>();
            if (enemy == null) return;

            // “寄り”は PlayShot + offset プリセットで代用
            Vector3 offset = new Vector3(1.2f, 1.5f, -2.0f);

            _camDir.PlayShot(
                enemy.transform,
                offset,
                move / Mathf.Max(0.01f, demoSpeed),
                hold / Mathf.Max(0.01f, demoSpeed),
                fov
            );
        }

        private void CameraOrbitEnemy(float radius, float height, float degrees, float fov, float seconds)
        {
            if (_camDir == null) return;
            var enemy = FindObjectOfType<EnemyController>();
            if (enemy == null) return;

            _camDir.PlayOrbit(
                enemy.transform,
                radius,
                height,
                degrees,
                fov,
                seconds / Mathf.Max(0.01f, demoSpeed)
            );
        }

        private void CameraPullBackEnding(float move, float hold, float fov)
        {
            if (_camDir == null) return;
            var player = FindObjectOfType<PlayerController>();
            if (player == null) return;

            // “引き”も PlayShot + offset プリセットで代用
            Vector3 offset = new Vector3(0.0f, 4.0f, -9.0f);

            _camDir.PlayShot(
                player.transform,
                offset,
                move / Mathf.Max(0.01f, demoSpeed),
                hold / Mathf.Max(0.01f, demoSpeed),
                fov
            );
        }
    }
}
