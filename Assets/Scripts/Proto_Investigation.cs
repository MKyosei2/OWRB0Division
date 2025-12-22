// Assets/Scripts/Proto_Investigation.cs
using System.Collections.Generic;
using UnityEngine;

namespace OJikaProto
{
    public enum EvidenceTag
    {
        None = 0,
        CCTV_Loop,
        StationStaff_Avoid,
        TicketGate_MemoryLoss,
        Clock_DeviceHint
    }

    public class InvestigationManager : SimpleSingleton<InvestigationManager>
    {
        private readonly HashSet<EvidenceTag> _tags = new();
        public int TargetCount { get; private set; } = 2;
        public int CollectedCount => _tags.Count;

        public void ResetForEpisode(int target)
        {
            _tags.Clear();
            TargetCount = Mathf.Max(0, target);
        }

        public void Add(EvidenceTag tag)
        {
            if (tag == EvidenceTag.None) return;
            if (_tags.Add(tag))
                EventBus.Instance?.Toast($"Evidence + {tag}");
        }

        public bool Has(EvidenceTag tag) => _tags.Contains(tag);
    }

    public class InvestigationPoint : MonoBehaviour
    {
        public EvidenceTag evidenceTag = EvidenceTag.CCTV_Loop;
        public float range = 2f;

        private bool _used;
        private Transform _player;

        private void Start()
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, range);
        }

        private void Update()
        {
            if (_used) return;

            if (_player == null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) _player = pc.transform;
                else return;
            }

            if (Vector3.Distance(_player.position, transform.position) > range) return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                InvestigationManager.Instance.Add(evidenceTag);
                _used = true;
                gameObject.SetActive(false);
            }
        }
    }
}
