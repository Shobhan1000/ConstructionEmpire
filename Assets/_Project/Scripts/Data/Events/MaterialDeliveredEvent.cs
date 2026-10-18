namespace ConstructionEmpire.Data.Events
{
    public readonly struct MaterialDeliveredEvent
    {
        public string OrderId { get; }
        public string ProjectId { get; }
        public string MaterialId { get; }
        public float Quantity { get; }

        public MaterialDeliveredEvent(string orderId, string projectId, string materialId, float quantity)
        {
            OrderId = orderId;
            ProjectId = projectId;
            MaterialId = materialId;
            Quantity = quantity;
        }
    }
}
