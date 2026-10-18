namespace ConstructionEmpire.Data.Events
{
    public readonly struct MaterialDelayedEvent
    {
        public string OrderId { get; }
        public string ProjectId { get; }
        public int DelayHours { get; }
        public string Reason { get; }

        public MaterialDelayedEvent(string orderId, string projectId, int delayHours, string reason)
        {
            OrderId = orderId;
            ProjectId = projectId;
            DelayHours = delayHours;
            Reason = reason;
        }
    }
}
