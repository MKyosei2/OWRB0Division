using System.Collections.Generic;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Step6: 契約ボーナスの最小実装。
    /// 契約成立後（メタで hasContractBoon=true）に、Qで「影の遮蔽」を生成。
    /// ・監視カメラのRayを遮る（潜入に効く）
    /// ・近接戦闘でも一瞬の逃げ/視線切りとして使える
    /// </summary>
    public class ContractBoonAbility : MonoBehaviour
    {
        [Header("Input")]
        public KeyCode key = KeyCode.Q;

        [Header("Shadow Cover")]
        public float cooldownSeconds = 8.0f;
        public float durationSeconds = 6.0f;
        public float spawnForward = 1.8f;
        public float spawnUp = 1.0f;
        public Vector3 coverScale = new Vector3(1.4f, 2.2f, 0.25f);
        public int maxSimultaneous = 2;

        private float _cd;
        private readonly Queue<GameObject> _covers = new();

        private void Update()
        {
            var meta = CaseMetaManager.Instance;
            if (meta == null || !meta.hasContractBoon) return;

            var flow = GameFlowController.Instance;
            if (flow != null && flow.State != FlowState.Playing) return;

            if (_cd > 0f) _cd -= Time.deltaTime;

            if (Input.GetKeyDown(key) && _cd <= 0f)
                SpawnCover();
        }

        private void SpawnCover()
        {
            _cd = Mathf.Max(0.15f, cooldownSeconds);

            Vector3 pos = transform.position + transform.forward * spawnForward + Vector3.up * spawnUp;
            Quaternion rot = Quaternion.LookRotation(transform.forward, Vector3.up);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShadowCover";
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = coverScale;

            // 見た目：暗め（軽いコントラスト）
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                // 既存マテリアル依存を避ける（プロトなので単色）
                r.sharedMaterial = new Material(Shader.Find("Standard"));
                r.sharedMaterial.color = new Color(0.06f, 0.08f, 0.10f, 1f);
            }

            // 物理：Raycastを遮るためColliderは残す
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;

            // 管理：出しすぎ防止
            _covers.Enqueue(go);
            while (_covers.Count > Mathf.Max(1, maxSimultaneous))
            {
                var old = _covers.Dequeue();
                if (old) Destroy(old);
            }

            Destroy(go, Mathf.Max(1.0f, durationSeconds));
            EventBus.Instance?.Toast("BOON: Shadow Cover");
        }
    }
}
