using System;

namespace OJikaProto
{
    /// <summary>
    /// Minimal save state for prototype resume.
    /// AI-free: recap text is provided by ProtoRecapDatabase templates.
    /// </summary>
    [Serializable]
    public class ProtoSaveState
    {
        public string caseId = "EP1";
        public string checkpointId = "EP1_START";

        // If true, show recap card on next launch.
        public bool wasInterrupted = false;

        // Most recent outcome (optional; used for outro/recap flavor)
        public NegotiationOutcome lastOutcome = NegotiationOutcome.None;

        // Shown in recap card (optional; can be empty)
        public string nextObjective = "";

        // Optional: show up to 2 rule tags (string IDs)
        public string[] ruleTags = new string[0];

        // Optional: a primary evidence card ID
        public string evidenceCardId = "";

        // Timestamp (unix seconds) for debug / recency
        public long savedAtUnix = 0;
    }
}
