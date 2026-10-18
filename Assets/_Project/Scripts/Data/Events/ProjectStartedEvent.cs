namespace ConstructionEmpire.Data.Events
{
    public readonly struct ProjectStartedEvent
    {
        public string ProjectId { get; }

        public ProjectStartedEvent(string projectId)
        {
            ProjectId = projectId;
        }
    }
}
