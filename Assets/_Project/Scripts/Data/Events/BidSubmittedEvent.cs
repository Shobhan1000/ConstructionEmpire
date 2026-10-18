namespace ConstructionEmpire.Data.Events
{
    public readonly struct BidSubmittedEvent
    {
        public string BidId { get; }
        public string ProjectId { get; }
        public float Amount { get; }

        public BidSubmittedEvent(string bidId, string projectId, float amount)
        {
            BidId = bidId;
            ProjectId = projectId;
            Amount = amount;
        }
    }
}
