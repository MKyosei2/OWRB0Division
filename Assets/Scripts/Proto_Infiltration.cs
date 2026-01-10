// Auto-updated: 2026-01-10
using System;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Step5: 捜査アクションの代表ミニゲーム（潜入/監視回避）
    /// - カメラの視界に入るとALERTが上昇
    /// - ALERTが最大になると一定時間「セキュリティ・ロックダウン」
    ///   （証拠回収ができない、ただしFail Forwardで解析が進む/行政コストが増える）
    /// </summary>
    public class InfiltrationManager : SimpleSingleton<InfiltrationManager>
    {
        [Header("Alert")]
        [Range(0f, 1f)] public float alert01 = 0f;
        public float alertRaisePerSecond = 1.25f;
        public float alertDecayPerSecond = 0.75f;

        [Header("Lockdown")]
        public float lockdownSeconds = 3.5f;
        [Range(0f, 1f)] public float adminCostOnLockdown = 0.10f;
        public int analysisPointsOnLockdown = 1;

        [Header("UX")]
        public float minTimeBetweenToasts = 0.8f;

        [Header("Audit (Step8)")]
        public bool auditEnabled = true;
        public float auditIntervalBase = 18f;
        public float auditDurationBase = 3.5f;
        [Range(0f, 1f)] public float auditAdminCost = 0.02f;

        public bool IsLockdownActive => Time.unscaledTime < _lockdownUntil;
        public float LockdownRemaining => Mathf.Max(0f, _lockdownUntil - Time.unscaledTime);

        public bool IsAuditActive => Time.unscaledTime < _auditUntil;
        public float AuditRemaining => Mathf.Max(0f, _auditUntil - Time.unscaledTime);
        public float AuditNextIn => (_nextAuditTime < 0f) ? 0f : Mathf.Max(0f, _nextAuditTime - Time.unscaledTime);

        /// <summary>Fires when alert changes (0..1).</summary>
        public event Action<float> OnAlertChanged;
        /// <summary>Fires when lockdown starts (reason, duration).</summary>
        public event Action<string, float> OnLockdownStarted;
        /// <summary>Fires when audit starts (level, duration).</summary>
        public event Action<int, float> OnAuditStarted;

        private float _lockdownUntil = -1f;
        private float _nextToastTime = -1f;
        private float _lastSeenTime = -999f;

        private float _auditUntil = -1f;
        private float _nextAuditTime = -1f;

        private float _lastAlert01 = -1f;

        /// <summary>
        /// Reset transient state (alert/lockdown/audit) after checkpoint warp or episode restart.
        /// Evidence/meta should persist; only moment-to-moment timers are cleared.
        /// </summary>
        public void ResetTransient(string reason = "")
        {
            alert01 = 0f;
            _lockdownUntil = -1f;
            _auditUntil = -1f;
            _nextAuditTime = -1f;
            _lastSeenTime = -999f;
            _lastAlert01 = -1f;
            OnAlertChanged?.Invoke(alert01);
            ProtoDiagnostics.TrackCounter("infiltration.reset", 1);
            if (!string.IsNullOrEmpty(reason))
                ProtoDiagnostics.Log("infiltration.reset.reason", $"Infiltration transient reset: {reason}", this);
        }

        // Used by ProtoCheckpointWarp via SendMessage.
        private void OnProtoCheckpointRestored(string checkpointId)
        {
            ResetTransient($"checkpoint:{checkpointId}");
        }

        /// <summary>
        /// Called by SecurityCameraCone when player is visible.
        /// </summary>
        public void ReportPlayerSeen(float intensity01 = 1f, string reason = "Camera")
        {
            if (!IsInInvestigationPhase()) return;

            _lastSeenTime = Time.unscaledTime;

            float before = alert01;
            float dt = Time.unscaledDeltaTime;
            alert01 = Mathf.Clamp01(alert01 + dt * alertRaisePerSecond * Mathf.Clamp01(intensity01));

            if (alert01 >= 0.999f)
            {
                TriggerLockdown(reason);
                alert01 = 0f; // reset after lockdown to avoid immediate re-trigger loops
            }

            if (!Mathf.Approximately(before, alert01))
                OnAlertChanged?.Invoke(alert01);
        }

        /// <summary>
        /// Force a security lockdown. Used both by alert overflow and debug tools.
        /// </summary>
        public void TriggerLockdown(string reason)
        {
            float secMul = 1f;

            // Step8: 期限/歪みで監視強化（行政コストも上がる）
            var meta = CaseMetaManager.Instance;
            if (meta != null)
                secMul = Mathf.Clamp(meta.GetSecurityMultiplier(), 1f, 1.7f);

            float sec = Mathf.Max(0.5f, lockdownSeconds * secMul);
            _lockdownUntil = Mathf.Max(_lockdownUntil, Time.unscaledTime + sec);

            // Fail Forward：失敗を「学習 + 行政コスト」に変換
            RunLogManager.Instance?.AddAdministrativeCost(adminCostOnLockdown * secMul);
            RuleManager.Instance?.GainInsightFromFailure(analysisPointsOnLockdown);

            ProtoDiagnostics.TrackCounter("infiltration.lockdown", 1);

            OnLockdownStarted?.Invoke(reason, sec);

            if (Time.unscaledTime >= _nextToastTime)
            {
                EventBus.Instance?.Toast($"SECURITY ALERT: {reason} / LOCKDOWN {LockdownRemaining:0.0}s");
                _nextToastTime = Time.unscaledTime + minTimeBetweenToasts;
            }
        }

        private void Update()
        {
            // Step8: 監査(AUDIT)の定期発生（調査フェーズのみ）
            if (auditEnabled && IsInInvestigationPhase())
            {
                int lvl = GetAuditLevel();
                if (lvl > 0)
                {
                    if (_nextAuditTime < 0f) _nextAuditTime = Time.unscaledTime + GetAuditIntervalSeconds(lvl);
                    if (Time.unscaledTime >= _nextAuditTime)
                    {
                        BeginAudit(lvl);
                        _nextAuditTime = Time.unscaledTime + GetAuditIntervalSeconds(lvl);
                    }
                }
                else
                {
                    _nextAuditTime = -1f;
                    _auditUntil = -1f;
                }
            }

            // 直近で視認されていないならALERT減衰
            bool seenRecently = (Time.unscaledTime - _lastSeenTime) < 0.12f;
            if (!seenRecently)
            {
                float decayMul = (auditEnabled && IsAuditActive) ? 0.45f : 1f;
                float before = alert01;
                float dt = Time.unscaledDeltaTime;
                alert01 = Mathf.Clamp01(alert01 - dt * alertDecayPerSecond * decayMul);

                if (!Mathf.Approximately(before, alert01))
                    OnAlertChanged?.Invoke(alert01);
            }

            // Push last alert value if needed
            if (_lastAlert01 < 0f) _lastAlert01 = alert01;
            if (!Mathf.Approximately(_lastAlert01, alert01))
            {
                _lastAlert01 = alert01;
                OnAlertChanged?.Invoke(alert01);
            }
        }

        private void BeginAudit(int lvl)
        {
            float secMul = 1f;
            var meta = CaseMetaManager.Instance;
            if (meta != null)
                secMul = Mathf.Clamp(meta.GetSecurityMultiplier(), 1f, 1.7f);

            float dur = GetAuditDurationSeconds(lvl) * secMul;
            _auditUntil = Mathf.Max(_auditUntil, Time.unscaledTime + dur);

            RunLogManager.Instance?.AddAdministrativeCost(auditAdminCost * secMul);
            OnAuditStarted?.Invoke(lvl, dur);

            ProtoDiagnostics.TrackCounter($"infiltration.audit.lv{lvl}", 1);

            if (Time.unscaledTime >= _nextToastTime)
            {
                EventBus.Instance?.Toast($"AUDIT START Lv{lvl} ({AuditRemaining:0.0}s)");
                _nextToastTime = Time.unscaledTime + minTimeBetweenToasts;
            }
        }

        private int GetAuditLevel()
        {
            var meta = CaseMetaManager.Instance;
            if (meta == null) return 0;
            // 「期限」は強く効き、「歪み」は少しだけ監査の面倒さを増やす
            int lvl = meta.truceDebt + Mathf.Max(0, meta.distortion - 1);
            return Mathf.Clamp(lvl, 0, 3);
        }

        private float GetAuditIntervalSeconds(int lvl)
        {
            float t = Mathf.Max(5f, auditIntervalBase - 2.2f * lvl);
            return t;
        }

        private float GetAuditDurationSeconds(int lvl)
        {
            float t = Mathf.Max(1.5f, auditDurationBase + 0.7f * lvl);
            return t;
        }

        private bool IsInInvestigationPhase()
        {
            var ep = FindObjectOfType<EpisodeController>();
            return ep != null && ep.Current != null && ep.Current.phaseType == EpisodePhaseType.Investigation;
        }
    }

    /// <summary>
    /// 監視カメラ（視界コーン）。プレイヤーがコーン内＆遮蔽物なしならALERTを上げる。
    /// </summary>
    public class SecurityCameraCone : MonoBehaviour
    {
        public Transform head;

        [Header("View")]
        public float viewDistance = 7f;
        [Range(10f, 170f)] public float viewAngle = 75f;

        [Header("Sweep")]
        public bool sweep = true;
        public float sweepAngle = 100f;
        public float sweepSpeed = 1.8f; // rad/sec

        [Header("Detection")]
        public LayerMask obstacleMask = ~0;
        public string reason = "Camera";
        [Range(0f, 1f)] public float seenIntensity01 = 1f;

        private Transform _player;
        private float _baseYaw;
        private float _nextFindPlayer = 0f;

        private void Start()
        {
            if (head == null) head = transform;
            _baseYaw = transform.eulerAngles.y;

            // Step8: 期限/歪みで監視カメラ自体が強化される（巡回強化）
            var meta = CaseMetaManager.Instance;
            if (meta != null)
            {
                float sm = Mathf.Clamp(meta.GetSecurityMultiplier(), 1f, 1.7f);
                float t = Mathf.Clamp01((sm - 1f) / 0.7f);

                viewDistance *= Mathf.Lerp(1f, 1.25f, t);
                sweepSpeed *= Mathf.Lerp(1f, 1.40f, t);
                sweepAngle *= Mathf.Lerp(1f, 1.20f, t);
                viewAngle = Mathf.Clamp(viewAngle * Mathf.Lerp(1f, 1.10f, t), 10f, 170f);
            }

            FindPlayer();
        }

        private void FindPlayer()
        {
            var pc = FindObjectOfType<PlayerController>();
            _player = pc ? pc.transform : null;
            _nextFindPlayer = Time.unscaledTime + 1.0f;
        }

        private void Update()
        {
            if (sweep)
            {
                float a = Mathf.Sin(Time.unscaledTime * sweepSpeed) * (sweepAngle * 0.5f);
                var e = transform.eulerAngles;
                e.y = _baseYaw + a;
                transform.eulerAngles = e;
            }

            if (_player == null)
            {
                if (Time.unscaledTime >= _nextFindPlayer) FindPlayer();
                return;
            }

            var inf = InfiltrationManager.Instance;
            if (inf == null) return;

            Vector3 origin = head.position;
            Vector3 target = _player.position + Vector3.up * 0.9f;
            Vector3 to = target - origin;

            float dist = to.magnitude;
            if (dist > viewDistance) return;

            Vector3 dir = to / Mathf.Max(0.001f, dist);
            float ang = Vector3.Angle(head.forward, dir);
            if (ang > viewAngle * 0.5f) return;

            // 遮蔽物チェック（当たったのがプレイヤー以外なら見えていない）
            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != _player && !hit.transform.IsChildOf(_player))
                    return;
            }

            inf.ReportPlayerSeen(seenIntensity01, reason);
        }

        private void OnDrawGizmosSelected()
        {
            Transform h = head ? head : transform;
            Vector3 origin = h.position;
            Vector3 fwd = h.forward;

            float half = viewAngle * 0.5f;
            Quaternion qL = Quaternion.AngleAxis(-half, Vector3.up);
            Quaternion qR = Quaternion.AngleAxis(half, Vector3.up);

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f);
            Gizmos.DrawLine(origin, origin + (qL * fwd) * viewDistance);
            Gizmos.DrawLine(origin, origin + (qR * fwd) * viewDistance);
            Gizmos.DrawLine(origin, origin + fwd * viewDistance);
            Gizmos.DrawWireSphere(origin, 0.08f);
        }
    }
}
