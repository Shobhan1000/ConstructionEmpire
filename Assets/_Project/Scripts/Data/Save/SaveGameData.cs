using System;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Time;

namespace ConstructionEmpire.Data.Save
{
    [Serializable, DataContract]
    public sealed class SaveGameData : SaveDataBase
    {
        [DataMember(IsRequired = true)] public CompanyData Company;
        [DataMember(IsRequired = true)] public GameTimeData Clock;
        [DataMember(IsRequired = true)] public int SimulationSeed;

        public void Validate()
        {
            if (Company == null || Clock == null)
                throw new SerializationException("Save must contain company and clock data.");
            Company.Validate();
            Clock.Validate();
        }

        public override void Migrate()
        {
            if (SchemaVersion != 1) { base.Migrate(); return; }
            // Nested DTOs are migrated separately by SaveGameService.
            if (Company == null || Clock == null)
                throw new SerializationException("Save must contain company and clock data.");
            SchemaVersion = 2;
        }
    }
}
