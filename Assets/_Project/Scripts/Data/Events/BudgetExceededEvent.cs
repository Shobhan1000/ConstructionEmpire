namespace ConstructionEmpire.Data.Events
{
    public readonly struct BudgetExceededEvent
    {
        public string ProjectId { get; }
        public float Budget { get; }
        public float ActualCost { get; }

        public BudgetExceededEvent(string projectId, float budget, float actualCost)
        {
            ProjectId = projectId;
            Budget = budget;
            ActualCost = actualCost;
        }
    }
}
