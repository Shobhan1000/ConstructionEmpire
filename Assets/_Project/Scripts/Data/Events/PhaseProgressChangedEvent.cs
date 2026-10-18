namespace ConstructionEmpire.Data.Events
{
    public readonly struct PhaseProgressChangedEvent
    {
        public string ProjectId { get; }
        public string PhaseId { get; }
        public double Progress { get; }
        public PhaseProgressChangedEvent(string projectId,string phaseId,double progress) { ProjectId=projectId; PhaseId=phaseId; Progress=progress; }
    }
}
