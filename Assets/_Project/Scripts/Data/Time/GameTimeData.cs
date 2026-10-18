using System;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;

namespace ConstructionEmpire.Data.Time
{
    [Serializable, DataContract]
    public sealed class GameTimeData : SaveDataBase
    {
        // One tick always advances one in-game hour.
        [DataMember(IsRequired = true)] public long TotalHours;
        [DataMember(IsRequired = true)] public double PendingSimulationSeconds;
        [DataMember(IsRequired = true)] public SimulationSpeed Speed = SimulationSpeed.Normal;
        [DataMember(IsRequired = true)] public double SecondsPerTick = SimulationDefaults.SecondsPerTick;
        [DataMember(IsRequired = true)] public int MaxTicksPerCall = SimulationDefaults.MaxTicksPerCall;
        [DataMember(IsRequired = true)] public int HoursPerDay = SimulationDefaults.HoursPerDay;
        [DataMember(IsRequired = true)] public int DaysPerWeek = SimulationDefaults.DaysPerWeek;
        [DataMember(IsRequired = true)] public int DaysPerMonth = SimulationDefaults.DaysPerMonth;

        public GameTimeData Copy() => (GameTimeData)MemberwiseClone();

        public void Validate()
        {
            if (TotalHours < 0 || double.IsNaN(PendingSimulationSeconds) ||
                double.IsInfinity(PendingSimulationSeconds) || PendingSimulationSeconds < 0d ||
                double.IsNaN(SecondsPerTick) || double.IsInfinity(SecondsPerTick) || SecondsPerTick <= 0d ||
                MaxTicksPerCall < 1 || HoursPerDay < 1 || DaysPerWeek < 1 || DaysPerMonth < 1 ||
                !Enum.IsDefined(typeof(SimulationSpeed), Speed))
                throw new SerializationException("Invalid clock state or simulation configuration.");
        }

        public override void Migrate()
        {
            if (SchemaVersion != 1) { base.Migrate(); return; }
            Validate();
            SchemaVersion = 2;
        }
    }
}
