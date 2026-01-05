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
        }
    }
}
