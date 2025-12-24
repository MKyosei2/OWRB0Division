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

        [Header("UI / Feedback")]
        public float minTimeBetweenToasts = 0.8f;

        private float _lastSeenTime = -999f;
        private float _lockdownUntil = -999f;
        private float _nextToastTime = -999f;

        public bool IsLockdownActive => Time.time < _lockdownUntil;
        public float LockdownRemaining => Mathf.Max(0f, _lockdownUntil - Time.time);

        /// <summary>
        /// カメラなどから「プレイヤーを視認した」通知。視認中は毎フレーム呼ばれてOK。
        /// </summary>
        public void ReportPlayerSeen(float intensity01 = 1f, string reason = "Camera")
        {
            _lastSeenTime = Time.time;

            float add = Time.deltaTime * alertRaisePerSecond * Mathf.Clamp01(intensity01);
            alert01 = Mathf.Clamp01(alert01 + add);

            if (alert01 >= 1f && !IsLockdownActive)
            {
                TriggerLockdown(reason);
                // 次の視認で即ロックダウン連打にならないよう少し戻す
                alert01 = 0.25f;
            }
        }

        public void TriggerLockdown(string reason)
        {
            float sec = Mathf.Max(0.5f, lockdownSeconds);
            _lockdownUntil = Mathf.Max(_lockdownUntil, Time.time + sec);

            // Fail Forward：失敗を「学習 + 行政コスト」に変換
            RunLogManager.Instance?.AddAdministrativeCost(adminCostOnLockdown);
            RuleManager.Instance?.GainInsightFromFailure(analysisPointsOnLockdown);

            if (Time.time >= _nextToastTime)
            {
                EventBus.Instance?.Toast($"SECURITY ALERT: {reason} / LOCKDOWN {LockdownRemaining:0.0}s");
                _nextToastTime = Time.time + minTimeBetweenToasts;
            }
        }

        private void Update()
        {
            // 直近で視認されていないならALERT減衰
            bool seenRecently = (Time.time - _lastSeenTime) < 0.12f;
            if (!seenRecently)
                alert01 = Mathf.Clamp01(alert01 - Time.deltaTime * alertDecayPerSecond);
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

        private void Start()
        {
            if (head == null) head = transform;
            _baseYaw = transform.eulerAngles.y;

            var pc = FindObjectOfType<PlayerController>();
            _player = pc ? pc.transform : null;
        }

        private void Update()
        {
            if (sweep)
            {
                float a = Mathf.Sin(Time.time * sweepSpeed) * (sweepAngle * 0.5f);
                var e = transform.eulerAngles;
                e.y = _baseYaw + a;
                transform.eulerAngles = e;
            }

            if (_player == null) return;
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
