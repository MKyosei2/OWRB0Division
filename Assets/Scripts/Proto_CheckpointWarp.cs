using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Optional utility: warp player to the next objective.
    /// Prototype-only convenience; can be stubbed.
    /// </summary>
    public class ProtoCheckpointWarp : MonoBehaviour
    {
        [Header("Optional warp points")]
        public Transform investigationPoint;
        public Transform combatStartPoint;

        public void WarpToNextObjective(ProtoSaveState state)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player == null) return;

            Transform target = null;
            if (state != null)
            {
                if (state.checkpointId == "EP1_INVEST") target = investigationPoint;
                else if (state.checkpointId == "EP1_BREAK") target = combatStartPoint;
            }

            if (target == null) return;

            player.transform.position = target.position;
            player.transform.rotation = target.rotation;

            // After warping, clear transient runtime states that often cause "stuck" / "lockdown persists" issues.
            string cp = (state != null) ? state.checkpointId : "";
            BroadcastCheckpointRestored(cp);
            ProtoDiagnostics.TrackCounter("checkpoint.warp", 1);
        }

        private void BroadcastCheckpointRestored(string checkpointId)
        {
            // 1) Known singletons
            InfiltrationManager.Instance?.ResetTransient($"warp:{checkpointId}");
            RuleManager.Instance?.ClearRuntime();
            SubtitleManager.Instance?.ClearAll();

            // 2) SendMessage-based hook (optional). Any MonoBehaviour can implement:
            //    void OnProtoCheckpointRestored(string checkpointId)
            // This avoids hard dependencies and keeps the prototype flexible.
            try
            {
                var behaviours = FindObjectsOfType<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                    behaviours[i].SendMessage("OnProtoCheckpointRestored", checkpointId, SendMessageOptions.DontRequireReceiver);
            }
            catch
            {
                // ignore
            }
        }
    }
}
