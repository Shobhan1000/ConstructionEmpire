using System;

namespace ConstructionEmpire.Data
{
    public enum CompanyLevel
    {
        SmallContractor = 1,
        LocalContractor = 2,
        RegionalContractor = 3,
        MajorContractor = 4,
        ConstructionCorporation = 5
    }

    /// <summary>
    /// Plain serializable data for the player's company (spec section 50,
    /// "Runtime Data"). This is what gets written to disk - it must never
    /// reference MonoBehaviours, ScriptableObjects, or other Unity engine
    /// objects, so it stays trivially saveable and unit-testable.
    /// </summary>
    [Serializable]
    public class CompanyData
    {
        public string CompanyName;
        public double Cash;
        public float Reputation;
        public string FoundedDateIso;
        public CompanyLevel Level = CompanyLevel.SmallContractor;

        public CompanyData Clone()
        {
            return new CompanyData
            {
                CompanyName = CompanyName,
                Cash = Cash,
                Reputation = Reputation,
                FoundedDateIso = FoundedDateIso,
                Level = Level
            };
        }
    }
}
