using System;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Construction
{
    public enum EquipmentType { PickupTruck, SmallExcavator, Bulldozer, WheelLoader, Crane, ConcreteMixer, DumpTruck, Forklift, ScissorLift, Telehandler, Compactor, Generator }
    public enum EquipmentStatus { Available, Assigned, InTransit, Working, Maintenance, Broken, RentedOut, Retired }
    [Serializable, DataContract]
    public sealed class EquipmentRuntimeData : SaveDataBase
    {
        public double FuelConsumptionMultiplier {get;set;}=1; public double BreakdownRateMultiplier {get;set;}=1;
        public double EffectiveFuelPerHour=>FuelPerHour*FuelConsumptionMultiplier;
        [DataMember] public string Id;
        [DataMember] public string DefinitionId;
        [DataMember] public string Name;
        [DataMember] public EquipmentType EquipmentType;
        [DataMember] public double PurchasePrice, RentalPricePerDay, MaintenanceCost;
        [DataMember] public EquipmentStatus Status;
        [DataMember] public bool IsRented;
        [DataMember] public bool DailyRentalContract;
        [DataMember] public double Condition;
        [DataMember] public string Location;
        [DataMember] public string CurrentProjectId;
        [DataMember] public string CurrentTaskId;
        [DataMember] public double CurrentFuel;
        [DataMember] public double FuelCapacity;
        [DataMember] public double FuelPerHour;
        [DataMember] public double CostPerHour;
        [DataMember] public double ProductivityMultiplier;
        public void Validate()
        {
            ConstructionValidation.Text(Id,nameof(Id)); ConstructionValidation.Text(DefinitionId,nameof(DefinitionId));
            ConstructionValidation.Text(Name,nameof(Name)); ConstructionValidation.Text(Location,nameof(Location));
            if (!Enum.IsDefined(typeof(EquipmentType),EquipmentType)) throw new ArgumentException("Unknown equipment type.");
            ConstructionValidation.NonNegative(PurchasePrice,nameof(PurchasePrice)); ConstructionValidation.NonNegative(RentalPricePerDay,nameof(RentalPricePerDay)); ConstructionValidation.NonNegative(MaintenanceCost,nameof(MaintenanceCost));
            if (!Enum.IsDefined(typeof(EquipmentStatus),Status)) throw new ArgumentException("Unknown equipment status.");
            ConstructionValidation.Unit(Condition,nameof(Condition)); ConstructionValidation.NonNegative(CurrentFuel,nameof(CurrentFuel));
            ConstructionValidation.NonNegative(FuelCapacity,nameof(FuelCapacity)); ConstructionValidation.NonNegative(FuelPerHour,nameof(FuelPerHour));
            ConstructionValidation.NonNegative(CostPerHour,nameof(CostPerHour)); ConstructionValidation.NonNegative(ProductivityMultiplier,nameof(ProductivityMultiplier));
            if (CurrentFuel > FuelCapacity) throw new ArgumentException("Fuel exceeds capacity.");
            if (string.IsNullOrEmpty(CurrentProjectId) != string.IsNullOrEmpty(CurrentTaskId)) throw new ArgumentException("Assignment needs project and task IDs.");
        }
        public EquipmentRuntimeData Copy() => (EquipmentRuntimeData)MemberwiseClone();
        public override void Migrate() { if (SchemaVersion!=1) { base.Migrate(); return; } Validate(); SchemaVersion=2; }
    }
}
