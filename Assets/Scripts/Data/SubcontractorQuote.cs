using System;

namespace ConstructionEmpire.Data
{
    /// <summary>An ephemeral quote shown to the player when comparing subcontractor options for a phase (spec section 18). Not persisted - regenerate on demand.</summary>
    [Serializable]
    public class SubcontractorQuote
    {
        public string SubcontractorId;
        public string CompanyName;
        public double Price;
        public int EstimatedDurationDays;
        public float Reliability;
        public float Quality;
    }
}
