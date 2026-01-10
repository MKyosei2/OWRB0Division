using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace OJikaProto
{
    public enum EvidenceTag
    {
        CCTV_Loop,
        StationStaff_Avoid,
        TicketGate_MemoryLoss,
        Clock_DeviceHint
    }

    public class InvestigationManager : SimpleSingleton<InvestigationManager>
    {
        private readonly HashSet<EvidenceTag> _collected = new();

        public int TargetCount { get; private set; } = 2;
        public int CollectedCount => _collected.Count;

        public void ResetForEpisode(int targetCount)
        {
            _collected.Clear();
            TargetCount = Mathf.Max(0, targetCount);
            EventBus.Instance?.Toast($"Investigation Reset ({CollectedCount}/{TargetCount})");
        }

        public bool Has(EvidenceTag tag) => _collected.Contains(tag);

        public void Collect(EvidenceTag tag)
        {
            if (_collected.Add(tag))
                EventBus.Instance?.Toast($"Evidence + {tag} ({CollectedCount}/{TargetCount})");
        }
    }

    [RequireComponent(typeof(Collider))]
    public class InvestigationPoint : MonoBehaviour
    {
        [Header("Evidence")]
        public EvidenceTag evidenceTag;
        public float interactRadius = 1.6f;

        [Header("Interaction")]
        public KeyCode interactKey = KeyCode.E;

        [Tooltip("触れただけで自動取得にする（E不要）")]
        public bool autoCollectOnTouch = false;

        [Tooltip("取得後にDestroyする（完全に消える）")]
        public bool destroyOnCollect = true;

        [Tooltip("Destroyしない場合、非表示にする")]
        public bool disableOnCollect = true;

        [Tooltip("非表示/Destroyまでの遅延（演出用）")]
        public float vanishDelay = 0.0f;

        [Header("Infiltration Mini-Game")]
        public bool requiresInfiltration = false;
        public bool blockedDuringLockdown = true;

        // runtime
        private bool _done;
        private Transform _player;
        private bool _playerInRange;

        private Collider _col;

        private void Awake()
        {
            _col = GetComponent<Collider>();
            _col.isTrigger = true; // AutoSetup向け
        }

        private void Start()
        {
            TryResolvePlayer(force: true);
        }

        private void Update()
        {
            if (_done) return;

            // ここが重要：Startで拾えなかった/あとから生成された時でも詰まらない
            if (_player == null)
                TryResolvePlayer(force: false);

            // プレイヤーが居ないなら何もできない（ログだけ出して終わり）
            if (_player == null) return;

            // autoCollectOnTouchがOFFなら、距離＋キーで取得
            if (!autoCollectOnTouch)
            {
                float d = Vector3.Distance(_player.position, transform.position);
                if (d <= interactRadius && IsInteractPressed())
                {
                    TryCollect();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_done) return;

            if (IsPlayerCollider(other))
            {
                _playerInRange = true;

                // autoなら入った瞬間に取得
                if (autoCollectOnTouch)
                    TryCollect();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayerCollider(other))
                _playerInRange = false;
        }

        private bool IsPlayerCollider(Collider other)
        {
            // PlayerControllerが付いてるオブジェクト（または親）をプレイヤーとみなす
            var pc = other.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                _player = pc.transform; // ここで確定させる
                return true;
            }
            return false;
        }

        private void TryResolvePlayer(bool force)
        {
            // 既に居るなら不要（forceで上書き可）
            if (!force && _player != null) return;

            var pc = FindObjectOfType<PlayerController>();
            _player = pc ? pc.transform : null;
        }

        private bool IsInteractPressed()
        {
            // 旧Input
            if (Input.GetKeyDown(interactKey)) return true;

#if ENABLE_INPUT_SYSTEM
            // 新InputSystemが有効ならこちらも拾う
            var kb = Keyboard.current;
            if (kb != null)
            {
                // interactKeyがE以外でも最低限Eは拾う（プロト用）
                if (kb.eKey.wasPressedThisFrame) return true;
            }
#endif
            return false;
        }

        private void TryCollect()
        {
            if (_done) return;

            // 侵入条件チェック
            var inf = InfiltrationManager.Instance;
            if (requiresInfiltration && blockedDuringLockdown && inf != null && inf.IsLockdownActive)
            {
                EventBus.Instance?.Toast($"SECURITY LOCKDOWN ({inf.LockdownRemaining:0.0}s)");
                return;
            }

            InvestigationManager.Instance?.Collect(evidenceTag);
            _done = true;

            // 取得フィードバック（縮小は残す）
            transform.localScale *= 0.35f;
            EventBus.Instance?.Toast("Evidence Collected");

            // 消す（期待通り「消える」）
            if (vanishDelay > 0f)
                Invoke(nameof(Vanish), vanishDelay);
            else
                Vanish();
        }

        private void Vanish()
        {
            if (destroyOnCollect)
            {
                Destroy(gameObject);
                return;
            }

            if (disableOnCollect)
            {
                // Renderer/Collider停止（見た目と当たりを消す）
                var rends = GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends) r.enabled = false;

                var cols = GetComponentsInChildren<Collider>(true);
                foreach (var c in cols) c.enabled = false;

                // 完全停止
                enabled = false;
            }
        }
    }
}
