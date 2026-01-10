using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// 撮影用：カメラの簡易ルート（F11で切替）。
    /// 注意：CaptureTools が許可されている時だけ動作。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class Proto_CameraRoute : MonoBehaviour
    {
        public enum RouteMode
        {
            Free,
            OrbitWide,
            OrbitClose,
            LockedWide
        }

        [Header("Guard")]
        [Tooltip("指定したシーン名の時だけ有効。空なら全シーン許可（非推奨）。")]
        public string[] allowedSceneNames = new[] { "Demo", "Prototype", "Title" };

        [Header("Toggle")]
        public KeyCode cycleKey = KeyCode.F11;
        public RouteMode mode = RouteMode.Free;

        [Header("Target")]
        public Transform target;
        public float targetHeight = 1.4f;

        [Header("OrbitWide")]
        public float wideRadius = 7.0f;
        public float wideHeight = 3.4f;
        public float wideSpeedDegPerSec = 18f;
        public float wideFov = 58f;

        [Header("OrbitClose")]
        public float closeRadius = 4.2f;
        public float closeHeight = 2.2f;
        public float closeSpeedDegPerSec = 28f;
        public float closeFov = 50f;

        [Header("LockedWide")]
        public Vector3 lockedOffset = new Vector3(0f, 4.2f, -7.0f);
        public float lockedFov = 58f;

        private Camera _cam;
        private Texture2D _flat;
        private GUIStyle _style;

        private bool AllowedNow()
        {
            if (!ProtoBuildConfig.AllowCaptureTools) return false;
            if (!ProtoBuildConfig.IsSceneAllowed(allowedSceneNames)) return false;
            return true;
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;

            _flat = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _flat.SetPixel(0, 0, Color.white);
            _flat.Apply();

            _style = new GUIStyle()
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };
            _style.normal.textColor = new Color(0.92f, 0.95f, 0.98f, 0.92f);

            if (!AllowedNow()) enabled = false;
        }

        private void Update()
        {
            if (!AllowedNow()) return;

            if (Input.GetKeyDown(cycleKey))
            {
                mode = (RouteMode)(((int)mode + 1) % 4);
                SubtitleManager.Instance?.Add($"CAM ROUTE : {mode}", 1.2f);
            }

            if (target == null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) target = pc.transform;
            }

            if (_cam == null) _cam = Camera.main;
        }

        private void LateUpdate()
        {
            if (!AllowedNow()) return;
            if (mode == RouteMode.Free) return;
            if (target == null || _cam == null) return;

            Vector3 focus = target.position + Vector3.up * targetHeight;

            switch (mode)
            {
                case RouteMode.OrbitWide:
                    Orbit(focus, wideRadius, wideHeight, wideSpeedDegPerSec, wideFov);
                    break;
                case RouteMode.OrbitClose:
                    Orbit(focus, closeRadius, closeHeight, closeSpeedDegPerSec, closeFov);
                    break;
                case RouteMode.LockedWide:
                    Locked(focus, lockedOffset, lockedFov);
                    break;
            }
        }

        private void Orbit(Vector3 focus, float radius, float height, float speedDeg, float fov)
        {
            float ang = Time.unscaledTime * speedDeg * Mathf.Deg2Rad;
            Vector3 off = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * radius + Vector3.up * height;

            _cam.transform.position = focus + off;
            _cam.transform.rotation = Quaternion.LookRotation((focus - _cam.transform.position).normalized, Vector3.up);
            _cam.fieldOfView = fov;
        }

        private void Locked(Vector3 focus, Vector3 offset, float fov)
        {
            _cam.transform.position = focus + offset;
            _cam.transform.rotation = Quaternion.LookRotation((focus - _cam.transform.position).normalized, Vector3.up);
            _cam.fieldOfView = fov;
        }

        private void OnGUI()
        {
            if (!AllowedNow()) return;
            if (mode == RouteMode.Free) return;

            Rect r = new Rect(12f, 12f, 240f, 24f);
            var prev = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.03f, 0.65f);
            GUI.DrawTexture(r, _flat);
            GUI.color = prev;

            GUI.Label(new Rect(r.x + 8, r.y + 4, r.width - 16, 18), $"CAM ROUTE: {mode} (F11)", _style);
        }
    }
}
