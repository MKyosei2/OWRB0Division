using UnityEngine;

namespace OJikaProto
{
    public class Proto_DebugTools : MonoBehaviour
    {
        private GameFlowController _flow;
        private EpisodeController _ep;

        private void Start()
        {
            _flow = FindObjectOfType<GameFlowController>();
            _ep = FindObjectOfType<EpisodeController>();
        }

        private void Update()
        {
            if (_flow == null) _flow = FindObjectOfType<GameFlowController>();
            if (_ep == null) _ep = FindObjectOfType<EpisodeController>();

            // É^ÉCÉgÉã/äÆóπíÜÇÕåÎçÏìÆÇ≥ÇπÇ»Ç¢
            if (_flow != null && _flow.State != FlowState.Playing) return;

            if (Input.GetKeyDown(KeyCode.F1))
            {
                _ep?.DebugJumpToCombat();
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                var enemy = FindObjectOfType<EnemyController>();
                if (enemy != null)
                {
                    var brk = enemy.GetComponent<Breakable>();
                    if (brk != null) brk.ApplyBreakDamage(99999f);
                    EventBus.Instance?.Toast("Debug: Enemy Broken");
                }
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
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
            }
        }
    }
}
