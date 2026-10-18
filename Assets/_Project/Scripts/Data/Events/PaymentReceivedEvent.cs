namespace ConstructionEmpire.Data.Events
{
    public readonly struct PaymentReceivedEvent
    {
        public string ProjectId { get; }
        public string PaymentId { get; }
        public float Amount { get; }

        public PaymentReceivedEvent(string projectId, string paymentId, float amount)
        {
            ProjectId = projectId;
            PaymentId = paymentId;
            Amount = amount;
        }
    }
}
