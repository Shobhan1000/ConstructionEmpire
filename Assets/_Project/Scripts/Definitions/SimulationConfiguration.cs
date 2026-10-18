using ConstructionEmpire.Data;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Time;
using UnityEngine;

namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName = "Construction Empire/Simulation Configuration")]
    public sealed class SimulationConfiguration : ScriptableObject
    {
        [Header("Provisional starting values - editable")]
        public string CompanyName = SimulationDefaults.CompanyName;
        [Min(0f)] public float StartingCash = SimulationDefaults.StartingCash;
        [Range(0f, 100f)] public float StartingReputation = SimulationDefaults.StartingReputation;
        public int Seed = SimulationDefaults.Seed;
        public ConstructionSettingsDefinition ConstructionSettings;
        [Header("Fixed tick scheduling (one tick = one game hour)")]
        [Min(0.0001f)] public float SecondsPerTick = (float)SimulationDefaults.SecondsPerTick;
        [Min(1)] public int MaxTicksPerCall = SimulationDefaults.MaxTicksPerCall;
        public SimulationSpeed InitialSpeed = SimulationSpeed.Normal;
        [Header("Provisional calendar")]
        [Min(1)] public int HoursPerDay = SimulationDefaults.HoursPerDay;
        [Min(1)] public int DaysPerWeek = SimulationDefaults.DaysPerWeek;
        [Min(1)] public int DaysPerMonth = SimulationDefaults.DaysPerMonth;

        public CompanyData CreateCompany() => new CompanyData(CompanyName, StartingCash, StartingReputation);
        public GameTimeData CreateClock() => new GameTimeData
        {
            SecondsPerTick = SecondsPerTick, MaxTicksPerCall = MaxTicksPerCall,
            HoursPerDay = HoursPerDay, DaysPerWeek = DaysPerWeek, DaysPerMonth = DaysPerMonth,
            Speed = InitialSpeed
        };
    }
}
