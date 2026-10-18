using System;
using ConstructionEmpire.Data.Events;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;

namespace ConstructionEmpire.Data.Company
{
    public enum TransactionResult { Applied, InsufficientFunds }

    [Serializable, DataContract]
    public sealed class CompanyData : SaveDataBase
    {
        public const float MinimumReputation = 0f;
        public const float MaximumReputation = 100f;

        [DataMember(Name = "CashBalance", IsRequired = true)]
        private float cashBalance;
        [DataMember(Name = "Reputation", IsRequired = true)]
        private float reputation;
        [DataMember(Name = "CompanyName", IsRequired = true)]
        private string companyName;

        [DataMember] private bool financeAllowsDeficit;

        public float CashBalance => cashBalance;
        public float Reputation => reputation;
        public string CompanyName => companyName;

        public CompanyData(string name, float startingCash, float startingReputation = SimulationDefaults.StartingReputation)
        {
            companyName = name;
            cashBalance = startingCash;
            reputation = startingReputation;
            Validate();
        }

        public TransactionResult ApplyTransaction(float amount, string reason)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A transaction reason is required.", nameof(reason));

            double proposed = (double)cashBalance + amount;
            if (proposed < 0d)
            {
                GameEvents.Publish(new TransactionRejectedEvent(this, amount, reason));
                return TransactionResult.InsufficientFunds;
            }
            if (proposed > float.MaxValue)
                throw new OverflowException("Transaction exceeds the supported cash range.");

            float previous = cashBalance;
            cashBalance = (float)proposed;
            GameEvents.Publish(new BalanceChangedEvent(this, previous, cashBalance, reason));
            return TransactionResult.Applied;
        }

        // Finance explicitly models deficits; ordinary ApplyTransaction still rejects unaffordable spending.
        public void SetFinanceCashBalance(float value,string reason)
        {
            if(float.IsNaN(value) || float.IsInfinity(value) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Invalid financial settlement.");
            float previous=cashBalance; financeAllowsDeficit=true; cashBalance=value;
            GameEvents.Publish(new BalanceChangedEvent(this,previous,value,reason));
        }
        public CompanyData Copy()=>(CompanyData)MemberwiseClone();
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Company name is required.");
            companyName = name.Trim();
            GameEvents.Publish(new CompanyIdentityChangedEvent(this));
        }
        public void SetReputation(float value, string reason)
        {
            if (float.IsNaN(value) || value < 0 || value > 100) throw new ArgumentOutOfRangeException(nameof(value));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.");
            float previous = reputation;
            if (previous == value) return;
            reputation = value;
            GameEvents.Publish(new ReputationChangedEvent(this, previous, value, reason));
        }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(companyName))
                throw new SerializationException("Company name is required.");
            if (float.IsNaN(cashBalance) || float.IsInfinity(cashBalance) || (cashBalance < 0f && !financeAllowsDeficit))
                throw new SerializationException("Cash must be finite and non-negative.");
            if (float.IsNaN(reputation) || reputation < MinimumReputation || reputation > MaximumReputation)
                throw new SerializationException("Reputation must be within 0-100.");
        }

        public override void Migrate()
        {
            if (SchemaVersion != 1) { base.Migrate(); return; }
            // v1 and v2 have the same fields in this foundation.
            Validate();
            SchemaVersion = 2;
        }
    }
}
