namespace ConstructionEmpire.Data.Events
{
    public readonly struct ProjectDelayedEvent
    {
        public string ProjectId { get; }
        public int DelayHours { get; }
        public string Reason { get; }

        public ProjectDelayedEvent(string projectId, int delayHours, string reason)
        {
            ProjectId = projectId;
            DelayHours = delayHours;
            Reason = reason;
        }
    }
}
