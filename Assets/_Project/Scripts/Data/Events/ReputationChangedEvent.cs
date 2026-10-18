using ConstructionEmpire.Data.Company;

namespace ConstructionEmpire.Data.Events
{
    public readonly struct ReputationChangedEvent
    {
        public CompanyData Company { get; }
        public float PreviousReputation { get; }
        public float CurrentReputation { get; }
        public string Reason { get; }

        public ReputationChangedEvent(CompanyData company, float previousReputation, float currentReputation, string reason)
        {
            Company = company;
            PreviousReputation = previousReputation;
            CurrentReputation = currentReputation;
            Reason = reason;
        }
    }
}
