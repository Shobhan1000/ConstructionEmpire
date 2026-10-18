namespace ConstructionEmpire.Data.Events
{
    public readonly struct EquipmentBreakdownEvent
    {
        public string EquipmentId { get; }
        public string ProjectId { get; }
        public string Reason { get; }

        public EquipmentBreakdownEvent(string equipmentId, string projectId, string reason)
        {
            EquipmentId = equipmentId;
            ProjectId = projectId;
            Reason = reason;
        }
    }
}
