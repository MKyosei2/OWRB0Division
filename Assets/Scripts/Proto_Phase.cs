using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Player-facing phase ("turn-based" feel).
    /// Keep this separate from internal EpisodePhaseType so we can present clear steps.
    /// </summary>
    public enum ProtoPhase
    {
        Story = 0,
        Investigation = 1,
        Combat = 2,
        Negotiation = 3,
        Result = 4,
    }
}
