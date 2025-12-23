using UnityEngine;

namespace OJikaProto
{
    public class ThirdPersonCameraRig : MonoBehaviour
    {
        public Transform target;
        public Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
        public float distance = 5.0f;
        public float height = 0.0f;

        public float yawSpeed = 220f;
        public float pitchSpeed = 160f;
        public float minPitch = -25f;
        public float maxPitch = 60f;

        private float _yaw;
        private float _pitch = 12f;

        private void LateUpdate()
        {
            if (!target) return;

            // マウス操作（Playing時はFlowがロックする想定）
            _yaw += Input.GetAxis("Mouse X") * yawSpeed * Time.unscaledDeltaTime;
            _pitch -= Input.GetAxis("Mouse Y") * pitchSpeed * Time.unscaledDeltaTime;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 pivot = target.position + pivotOffset + Vector3.up * height;
            Vector3 camPos = pivot - (rot * Vector3.forward) * distance;

            transform.position = camPos;
            transform.rotation = rot;
        }
    }
}
