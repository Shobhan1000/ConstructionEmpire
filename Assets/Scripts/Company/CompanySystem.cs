using System;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Persistence;

namespace ConstructionEmpire.CompanyManagement
{
    /// <summary>
    /// Owns the player's company data and enforces the rules around it -
    /// cash can't go negative through normal spending, reputation is
    /// clamped 0-100, etc. UI and other systems should go through this
    /// class rather than mutating CompanyData directly. Deliberately plain
    /// C# (no MonoBehaviour) so it's testable without a scene - see
    /// Tests/EditMode/CompanySystemTests.cs.
    /// </summary>
    public class CompanySystem : ISaveable<CompanyData>
    {
        public CompanyData Data { get; private set; }
        public bool HasCompany => Data != null;

        public void FoundCompany(string companyName, double startingCash, float startingReputation, DateTime foundedDate)
        {
            Data = new CompanyData
            {
                CompanyName = companyName,
                Cash = startingCash,
                Reputation = ClampReputation(startingReputation),
                FoundedDateIso = foundedDate.ToString("o"),
                Level = CompanyLevel.SmallContractor
            };

            EventBus.Publish(new CompanyFoundedEvent(companyName, startingCash));
        }

        public void AddCash(double amount, string reason)
        {
            if (Data == null || amount <= 0) return;

            Data.Cash += amount;
            EventBus.Publish(new CompanyCashChangedEvent(Data.Cash, amount, reason));
        }

        /// <summary>Attempts to spend cash. Returns false (and spends nothing) if funds are insufficient - callers must handle that rather than letting cash go negative.</summary>
        public bool TrySpendCash(double amount, string reason)
        {
            if (Data == null || amount <= 0) return false;
            if (Data.Cash < amount) return false;

            Data.Cash -= amount;
            EventBus.Publish(new CompanyCashChangedEvent(Data.Cash, -amount, reason));
            return true;
        }

        public void AdjustReputation(float delta, string reason)
        {
            if (Data == null) return;

            float newReputation = ClampReputation(Data.Reputation + delta);
            float actualDelta = newReputation - Data.Reputation;
            Data.Reputation = newReputation;

            EventBus.Publish(new CompanyReputationChangedEvent(Data.Reputation, actualDelta, reason));
        }

        private static float ClampReputation(float value)
        {
            if (value < 0f) return 0f;
            if (value > 100f) return 100f;
            return value;
        }

        public CompanyData CaptureState() => Data?.Clone();

        public void RestoreState(CompanyData state)
        {
            Data = state?.Clone();
        }
    }
}
