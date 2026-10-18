namespace ConstructionEmpire.Data.Events
{
    public readonly struct BidWonEvent
    {
        public string BidId { get; }
        public string ProjectId { get; }

        public BidWonEvent(string bidId, string projectId)
        {
            BidId = bidId;
            ProjectId = projectId;
        }
    }
}
