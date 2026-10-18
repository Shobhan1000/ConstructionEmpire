using System;
using System.Collections.Generic;

namespace ConstructionEmpire.Data
{
    [Serializable]
    public class ProjectRuntimeData
    {
        public string Id;
        public ProjectType Type;
        public string DisplayName;
        public string ClientId;
        public string ClientName;
        public double ContractValue;
        public string DeadlineIso;
        public string StartDateIso;
        public string CompletedDateIso;
        public ProjectStatus Status = ProjectStatus.Available;
        public List<PhaseRuntimeData> Phases = new List<PhaseRuntimeData>();

        public double CashReceivedToDate;
        public double CostIncurredToDate;
        public bool UpfrontPaid;
        public bool MilestonePaid;

        /// <summary>Weighted average of completed phases' QualityScore (see QualityScoring). 0 until the first phase completes.</summary>
        public float QualityScore;
    }
}
