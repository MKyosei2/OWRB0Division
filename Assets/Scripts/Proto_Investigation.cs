using System.Collections.Generic;
using UnityEngine;

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
        public EvidenceTag evidenceTag;
        public float interactRadius = 1.6f;


        [Header("Infiltration Mini-Game")]
        public bool requiresInfiltration = false;
        public bool blockedDuringLockdown = true;
        private bool _done;
        private Transform _player;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true; // AutoSetup‚ÌCube‚É‚àŒø‚­
        }

        private void Start()
        {
            var pc = FindObjectOfType<PlayerController>();
            _player = pc ? pc.transform : null;
        }

        private void Update()
        {
            if (_done) return;
            if (_player == null) return;

            float d = Vector3.Distance(_player.position, transform.position);
            if (d <= interactRadius && Input.GetKeyDown(KeyCode.E))
            {
                var inf = InfiltrationManager.Instance;
                if (requiresInfiltration && blockedDuringLockdown && inf != null && inf.IsLockdownActive)
                {
                    EventBus.Instance?.Toast($"SECURITY LOCKDOWN ({inf.LockdownRemaining:0.0}s)");
                    return;
                }

                InvestigationManager.Instance?.Collect(evidenceTag);
                _done = true;

                // Œ©‚½–Ú‚Å•ª‚©‚é‚æ‚¤‚É¬‚³‚­‚·‚é
                transform.localScale *= 0.35f;
                EventBus.Instance?.Toast("Evidence Collected");
            }
        }
    }
}
