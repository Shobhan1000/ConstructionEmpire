using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Systems.Construction
{
    public readonly struct ProgressFactors
    {
        public double BaseProductivity { get; }
        public double LaborEfficiency { get; }
        public double EquipmentEfficiency { get; }
        public double MaterialAvailability { get; }
        public double SiteConditions { get; }
        public double WeatherModifier { get; }
        // Output is percentage points per fixed game hour.
        public double Progress => BaseProductivity * LaborEfficiency * EquipmentEfficiency * MaterialAvailability * SiteConditions * WeatherModifier;
        public ProgressFactors(double baseProductivity,double laborEfficiency,double equipmentEfficiency,double materialAvailability,double siteConditions=1,double weatherModifier=1)
        {
            ConstructionValidation.NonNegative(baseProductivity,nameof(baseProductivity)); ConstructionValidation.NonNegative(laborEfficiency,nameof(laborEfficiency));
            ConstructionValidation.NonNegative(equipmentEfficiency,nameof(equipmentEfficiency)); ConstructionValidation.Unit(materialAvailability,nameof(materialAvailability));
            ConstructionValidation.NonNegative(siteConditions,nameof(siteConditions)); ConstructionValidation.NonNegative(weatherModifier,nameof(weatherModifier));
            BaseProductivity=baseProductivity; LaborEfficiency=laborEfficiency; EquipmentEfficiency=equipmentEfficiency;
            MaterialAvailability=materialAvailability; SiteConditions=siteConditions; WeatherModifier=weatherModifier;
            ConstructionValidation.NonNegative(Progress,nameof(Progress));
        }
    }
}
