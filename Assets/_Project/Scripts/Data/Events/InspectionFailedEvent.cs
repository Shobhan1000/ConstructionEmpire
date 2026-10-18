namespace ConstructionEmpire.Data.Events
{
    public readonly struct InspectionFailedEvent
    {
        public string ProjectId { get; }
        public string InspectionId { get; }
        public string Reason { get; }

        public InspectionFailedEvent(string projectId, string inspectionId, string reason)
        {
            ProjectId = projectId;
            InspectionId = inspectionId;
            Reason = reason;
        }
    }
}
