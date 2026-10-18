using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Construction
{
    [Serializable, DataContract]
    public sealed class EquipmentRequirement
    {
        [DataMember] public EquipmentType Type;
        [DataMember] public int Count;
        public EquipmentRequirement() { }
        public EquipmentRequirement(EquipmentType type,int count) { Type=type; Count=count; }
    }
    [Serializable, DataContract]
    public sealed class ConstructionPhaseRuntimeData : SaveDataBase
    {
        [DataMember] public bool RequiresSubcontractor;
        [DataMember] public ConstructionEmpire.Data.Subcontractors.SubcontractorSpecialty SubcontractorSpecialty;
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember] public double DurationHours;
        [DataMember] public double LaborHours;
        [DataMember] public List<WorkerType> CompatibleTrades = new List<WorkerType>();
        [DataMember] public List<EquipmentRequirement> EquipmentRequirements = new List<EquipmentRequirement>();
        [DataMember] public List<MaterialQuantity> MaterialRequirements = new List<MaterialQuantity>();
        [DataMember] public List<string> PrerequisiteIds = new List<string>();
        [DataMember] public double Cost;
        [DataMember] public double QualityImpact;
        [DataMember] public double Risk;
        [DataMember] public double ReworkHoursRemaining;
        [DataMember] public double Progress;
        [DataMember] public double AccruedCost;
        public bool IsComplete => Progress >= 100;
        public void Validate()
        {
            if(RequiresSubcontractor && !Enum.IsDefined(typeof(ConstructionEmpire.Data.Subcontractors.SubcontractorSpecialty),SubcontractorSpecialty)) throw new ArgumentException("Invalid subcontractor specialty.");
            ConstructionValidation.Text(Id,nameof(Id)); ConstructionValidation.Text(Name,nameof(Name));
            ConstructionValidation.Positive(DurationHours,nameof(DurationHours)); ConstructionValidation.Positive(LaborHours,nameof(LaborHours));
            ConstructionValidation.NonNegative(ReworkHoursRemaining,nameof(ReworkHoursRemaining));
            ConstructionValidation.NonNegative(Cost,nameof(Cost)); ConstructionValidation.NonNegative(AccruedCost,nameof(AccruedCost));
            ConstructionValidation.Unit(Risk,nameof(Risk)); ConstructionValidation.Percent(Progress,nameof(Progress));
            if(double.IsNaN(QualityImpact)||double.IsInfinity(QualityImpact)) throw new ArgumentOutOfRangeException(nameof(QualityImpact));
            if(PrerequisiteIds==null || CompatibleTrades==null || EquipmentRequirements==null || MaterialRequirements==null) throw new ArgumentException("Phase lists are required.");
            var dependencies=new HashSet<string>(StringComparer.Ordinal);
            foreach(var id in PrerequisiteIds) { ConstructionValidation.Text(id,"PrerequisiteId"); if(!dependencies.Add(id)||id==Id) throw new ArgumentException("Duplicate or self prerequisite."); }
            var types=new HashSet<EquipmentType>();
            foreach(var r in EquipmentRequirements) if(r==null || !Enum.IsDefined(typeof(EquipmentType),r.Type) || r.Count<=0 || !types.Add(r.Type)) throw new ArgumentException("Invalid equipment requirements.");
            var materials=new HashSet<string>(StringComparer.Ordinal);
            foreach(var r in MaterialRequirements) { if(r==null) throw new ArgumentException("Null material."); ConstructionValidation.Text(r.MaterialId,"MaterialId"); ConstructionValidation.Positive(r.Quantity,"Quantity"); if(!materials.Add(r.MaterialId)) throw new ArgumentException("Duplicate material requirement."); }
            foreach(var trade in CompatibleTrades) if(!Enum.IsDefined(typeof(WorkerType),trade)) throw new ArgumentException("Invalid trade.");
        }
        public ConstructionPhaseRuntimeData Copy()
        {
            var copy=(ConstructionPhaseRuntimeData)MemberwiseClone();
            copy.PrerequisiteIds=new List<string>(PrerequisiteIds); copy.CompatibleTrades=new List<WorkerType>(CompatibleTrades);
            copy.EquipmentRequirements=EquipmentRequirements.ConvertAll(x=>new EquipmentRequirement(x.Type,x.Count));
            copy.MaterialRequirements=MaterialRequirements.ConvertAll(x=>new MaterialQuantity(x.MaterialId,x.Quantity)); return copy;
        }
        public override void Migrate() { if(SchemaVersion!=1) { base.Migrate(); return; } Validate(); SchemaVersion=2; }
    }
}
