// Auto-updated: 2026-01-10
using System;
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

    /// <summary>
    /// Evidence collection store for the current episode.
    /// NOTE: keep this deterministic (no RNG) for prototype stability.
    /// </summary>
    public class InvestigationManager : SimpleSingleton<InvestigationManager>
    {
        private readonly HashSet<EvidenceTag> _collected = new();

        public int TargetCount { get; private set; } = 2;
        public int CollectedCount => _collected.Count;
        public bool IsComplete => CollectedCount >= TargetCount;

        /// <summary>Fires when a new evidence tag is collected.</summary>
        public event Action<EvidenceTag> OnEvidenceCollected;

        public void ResetForEpisode(int targetCount)
        {
            _collected.Clear();
            TargetCount = Mathf.Max(0, targetCount);
            EventBus.Instance?.Toast($"Investigation Reset ({CollectedCount}/{TargetCount})");
            ProtoDiagnostics.TrackCounter("invest.reset", 1);
        }

        public bool Has(EvidenceTag tag) => _collected.Contains(tag);

        public IReadOnlyCollection<EvidenceTag> Collected => _collected;

        public void Collect(EvidenceTag tag)
        {
            if (_collected.Add(tag))
            {
                EventBus.Instance?.Toast($"Evidence + {tag} ({CollectedCount}/{TargetCount})");
                OnEvidenceCollected?.Invoke(tag);
                ProtoDiagnostics.TrackCounter($"evidence.{tag}", 1);
            }
        }
    }

    [RequireComponent(typeof(Collider))]
    public class InvestigationPoint : MonoBehaviour
    {
        [Header("Evidence")]
        public EvidenceTag evidenceTag;
        public float interactRadius = 1.6f;
        public KeyCode interactKey = KeyCode.E;

        [Header("Infiltration Mini-Game")]
        public bool requiresInfiltration = false;
        public bool blockedDuringLockdown = true;

        [Header("UX")]
        [Tooltip("If true, shrinks after collection to visually indicate completion.")]
        public bool shrinkOnCollected = true;

        private bool _done;
        private Transform _player;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true; // AutoSetupのCubeにも効く
        }

        private void Start()
        {
            var pc = FindObjectOfType<PlayerController>();
            _player = pc ? pc.transform : null;

            // If this evidence was already collected (save/load/checkpoint), mark done.
            var im = InvestigationManager.Instance;
            if (im != null && im.Has(evidenceTag))
            {
                _done = true;
                if (shrinkOnCollected)
                    transform.localScale *= 0.35f;
            }

            if (_player == null)
                ProtoDiagnostics.Warn("invest.no_player", "InvestigationPoint: Player not found", this);
        }

        private void Update()
        {
            if (_done) return;
            if (_player == null) return;

            float d = Vector3.Distance(_player.position, transform.position);
            if (d > interactRadius) return;

            // In-world prompt can be handled by UI later; keep logic here.
            if (!Input.GetKeyDown(interactKey)) return;

            var inf = InfiltrationManager.Instance;
            if (requiresInfiltration && blockedDuringLockdown && inf != null && inf.IsLockdownActive)
            {
                EventBus.Instance?.Toast($"SECURITY LOCKDOWN ({inf.LockdownRemaining:0.0}s)");
                return;
            }

            InvestigationManager.Instance?.Collect(evidenceTag);
            _done = true;

            if (shrinkOnCollected)
            {
                // 見た目で分かるように小さくする
                transform.localScale *= 0.35f;
            }

            EventBus.Instance?.Toast("Evidence Collected");
        }

        // Called by ProtoCheckpointWarp (or any system) after a checkpoint warp.
        // Keeps points from being re-collectable when the evidence already exists.
        private void OnProtoCheckpointRestored(string checkpointId)
        {
            var im = InvestigationManager.Instance;
            if (im != null && im.Has(evidenceTag))
            {
                _done = true;
                if (shrinkOnCollected)
                    transform.localScale *= 0.35f;
            }
        }
    }
}
