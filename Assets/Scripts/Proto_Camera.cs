// Assets/Scripts/Proto_Camera.cs
using UnityEngine;

namespace OJikaProto
{
    public class ThirdPersonCameraRig : MonoBehaviour
    {
        public Transform target;
        public Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
        public float distance = 4.5f;

        public float yawSpeed = 180f;
        public float pitchSpeed = 120f;
        public float minPitch = -25f;
        public float maxPitch = 65f;

        private float _yaw;
        private float _pitch = 20f;

        private void Start()
        {
            if (target == null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) target = pc.transform;
            }

            var e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = Mathf.Clamp(e.x, minPitch, maxPitch);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            _yaw += mx * yawSpeed * Time.deltaTime;
            _pitch -= my * pitchSpeed * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;

            Vector3 camPos = pivot + rot * new Vector3(0f, 0f, -distance);
            transform.position = camPos;
            transform.rotation = rot;

            transform.LookAt(pivot);
        }
    }
}
