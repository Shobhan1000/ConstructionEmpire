namespace ConstructionEmpire.Data.Events
{
    public readonly struct ProjectCompletedEvent
    {
        public string ProjectId { get; }

        public ProjectCompletedEvent(string projectId)
        {
            ProjectId = projectId;
        }
    }
}
