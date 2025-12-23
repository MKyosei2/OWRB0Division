using System.Collections;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Cinemachine無しで「寄り/引き/回り込み/ツーショット」を作る簡易カメラ演出。
    /// ThirdPersonCameraRigを一時停止して、カメラを直接制御→戻す。
    /// </summary>
    public class Proto_CameraDirector : MonoBehaviour
    {
        [Header("Defaults")]
        public float defaultFov = 55f;
        public float ease = 7.5f;

        private Camera _cam;
        private ThirdPersonCameraRig _rig;
        private bool _rigWasEnabled;

        private Coroutine _co;
        private float _baseFov;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;

            _rig = (_cam != null) ? _cam.GetComponent<ThirdPersonCameraRig>() : null;
            if (_cam != null) _baseFov = _cam.fieldOfView;
        }

        public void Cancel()
        {
            if (_co != null) StopCoroutine(_co);
            _co = null;
            EndCinematic();
        }

        public void BeginCinematic()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (_rig == null) _rig = _cam.GetComponent<ThirdPersonCameraRig>();

            if (_rig != null)
            {
                _rigWasEnabled = _rig.enabled;
                _rig.enabled = false;
            }

            if (_cam != null && _baseFov <= 0f) _baseFov = _cam.fieldOfView;
        }

        public void EndCinematic()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (_rig != null) _rig.enabled = _rigWasEnabled;

            if (_cam != null) _cam.fieldOfView = (_baseFov > 0f) ? _baseFov : defaultFov;
        }

        /// <summary>
        /// 固定ショット：focusを見ながら、offset位置に置く（ワールド座標）。
        /// </summary>
        public void PlayShot(Transform focus, Vector3 offsetWorld, float fov, float moveSeconds, float holdSeconds)
        {
            if (focus == null) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(ShotCo(focus, offsetWorld, fov, moveSeconds, holdSeconds));
        }

        /// <summary>
        /// 回り込みショット：focusの周りを角度だけ回す（半径＋高さ）。
        /// </summary>
        public void PlayOrbit(Transform focus, float radius, float height, float degrees, float fov, float seconds)
        {
            if (focus == null) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(OrbitCo(focus, radius, height, degrees, fov, seconds));
        }

        private IEnumerator ShotCo(Transform focus, Vector3 offsetWorld, float fov, float moveSeconds, float holdSeconds)
        {
            BeginCinematic();

            if (_cam == null) yield break;

            Vector3 startPos = _cam.transform.position;
            Quaternion startRot = _cam.transform.rotation;
            float startFov = _cam.fieldOfView;

            Vector3 targetPos = focus.position + offsetWorld;

            float t = 0f;
            float dur = Mathf.Max(0.01f, moveSeconds);

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / dur);
                a = Smooth(a);

                Vector3 pos = Vector3.Lerp(startPos, targetPos, a);
                _cam.transform.position = pos;

                Quaternion rot = Quaternion.LookRotation((focus.position + Vector3.up * 1.2f) - pos, Vector3.up);
                _cam.transform.rotation = Quaternion.Slerp(startRot, rot, a);

                _cam.fieldOfView = Mathf.Lerp(startFov, fov, a);
                yield return null;
            }

            // hold
            float hold = Mathf.Max(0f, holdSeconds);
            float ht = 0f;
            while (ht < hold)
            {
                ht += Time.unscaledDeltaTime;

                // 追従（少しだけ）
                Vector3 pos = Vector3.Lerp(_cam.transform.position, focus.position + offsetWorld, Time.unscaledDeltaTime * ease);
                _cam.transform.position = pos;
                _cam.transform.rotation = Quaternion.LookRotation((focus.position + Vector3.up * 1.2f) - pos, Vector3.up);

                yield return null;
            }

            _co = null;
        }

        private IEnumerator OrbitCo(Transform focus, float radius, float height, float degrees, float fov, float seconds)
        {
            BeginCinematic();

            if (_cam == null) yield break;

            float dur = Mathf.Max(0.05f, seconds);
            float startAngle = 0f;

            // 現在位置から角度を推定
            Vector3 from = _cam.transform.position - focus.position;
            from.y = 0f;
            if (from.sqrMagnitude > 0.001f)
            {
                startAngle = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
            }

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / dur);
                float ang = startAngle + degrees * a;

                Vector3 pos = focus.position + new Vector3(
                    Mathf.Sin(ang * Mathf.Deg2Rad) * radius,
                    height,
                    Mathf.Cos(ang * Mathf.Deg2Rad) * radius
                );

                _cam.transform.position = Vector3.Lerp(_cam.transform.position, pos, Time.unscaledDeltaTime * ease);
                _cam.transform.rotation = Quaternion.LookRotation((focus.position + Vector3.up * 1.2f) - _cam.transform.position, Vector3.up);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, Time.unscaledDeltaTime * ease);

                yield return null;
            }

            _co = null;
        }

        private static float Smooth(float a)
        {
            // smoothstep
            return a * a * (3f - 2f * a);
        }
    }
}
