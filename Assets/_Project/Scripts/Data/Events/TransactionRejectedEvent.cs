using ConstructionEmpire.Data.Company;

namespace ConstructionEmpire.Data.Events
{
    public readonly struct TransactionRejectedEvent
    {
        public CompanyData Company { get; }
        public float Amount { get; }
        public string Reason { get; }

        public TransactionRejectedEvent(CompanyData company, float amount, string reason)
        {
            Company = company;
            Amount = amount;
            Reason = reason;
        }
    }
}
