using ConstructionEmpire.Data.Company;

namespace ConstructionEmpire.Data.Events
{
    public readonly struct BalanceChangedEvent
    {
        public CompanyData Company { get; }
        public float PreviousBalance { get; }
        public float CurrentBalance { get; }
        public string Reason { get; }

        public BalanceChangedEvent(CompanyData company, float previousBalance, float currentBalance, string reason)
        {
            Company = company;
            PreviousBalance = previousBalance;
            CurrentBalance = currentBalance;
            Reason = reason;
        }
    }
}
