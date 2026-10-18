namespace ConstructionEmpire.Data.Events
{
    public readonly struct BidLostEvent
    {
        public string BidId { get; }
        public string ProjectId { get; }
        public string Reason { get; }

        public BidLostEvent(string bidId, string projectId, string reason)
        {
            BidId = bidId;
            ProjectId = projectId;
            Reason = reason;
        }
    }
}
