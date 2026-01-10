// Auto-updated: 2026-01-09
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Developer hotkeys for quick iteration.
    /// Disabled automatically in PROTO_PUBLIC_BUILD / non-debug builds.
    /// </summary>
    public class Proto_DebugTools : MonoBehaviour
    {
        private GameFlowController _flow;
        private EpisodeController _ep;

        private void Awake()
        {
            if (!ProtoBuildConfig.AllowDebugHotkeys)
            {
                enabled = false;
            }
        }

        private void Start()
        {
            RefreshRefs();
        }

        private void RefreshRefs()
        {
            if (_flow == null) _flow = FindObjectOfType<GameFlowController>();
            if (_ep == null) _ep = FindObjectOfType<EpisodeController>();
        }

        private void Update()
        {
            if (!ProtoBuildConfig.AllowDebugHotkeys) return;

            if (_flow == null || _ep == null)
                RefreshRefs();

            if (Input.GetKeyDown(KeyCode.F1))
            {
                // Toggle flow diagram overlay (if present)
                var ov = FindObjectOfType<Proto_FlowDiagramOverlay>();
                if (ov != null) ov.enabled = !ov.enabled;
                EventBus.Instance?.Toast($"Debug: FlowOverlay {(ov != null && ov.enabled ? "ON" : "OFF")}");
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                // Force episode start (Case01)
                _flow?.StartGame();
                EventBus.Instance?.Toast("Debug: Start Case01");
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                // Give all evidence
                var im = InvestigationManager.Instance;
                if (im != null)
                {
                    im.Collect(EvidenceTag.CCTV_Loop);
                    im.Collect(EvidenceTag.StationStaff_Avoid);
                    im.Collect(EvidenceTag.TicketGate_MemoryLoss);
                    im.Collect(EvidenceTag.Clock_DeviceHint);
                    EventBus.Instance?.Toast("Debug: Evidence All");
                }
            }

            if (Input.GetKeyDown(KeyCode.F4))
            {
                RunLogManager.Instance?.LogViolation("DEBUG", "forced violation");
                EventBus.Instance?.RuleViolated("DEBUG", "forced violation", 0.85f);
                EventBus.Instance?.Toast("Debug: Violation");
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                // Force lockdown (to test fail-forward)
                InfiltrationManager.Instance?.TriggerLockdown("DEBUG");
            }
        }
    }
}
