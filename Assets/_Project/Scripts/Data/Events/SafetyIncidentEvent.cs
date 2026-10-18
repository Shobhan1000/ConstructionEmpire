namespace ConstructionEmpire.Data.Events
{
    public readonly struct SafetyIncidentEvent
    {
        public string ProjectId { get; }
        public string IncidentId { get; }
        public string Description { get; }

        public SafetyIncidentEvent(string projectId, string incidentId, string description)
        {
            ProjectId = projectId;
            IncidentId = incidentId;
            Description = description;
        }
    }
}
