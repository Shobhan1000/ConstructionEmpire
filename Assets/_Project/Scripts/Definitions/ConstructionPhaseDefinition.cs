using System.Collections.Generic;
using ConstructionEmpire.Data.Construction;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/Construction Phase")]
    public sealed class ConstructionPhaseDefinition : ScriptableObject
    {
        public bool RequiresSubcontractor;
        public ConstructionEmpire.Data.Subcontractors.SubcontractorSpecialty SubcontractorSpecialty;
        public string PhaseId;
        public string DisplayName;
        public double DurationHours;
        public double LaborHours;
        public List<WorkerType> CompatibleTrades=new List<WorkerType>();
        public List<EquipmentRequirement> EquipmentRequirements=new List<EquipmentRequirement>();
        public List<MaterialQuantity> MaterialRequirements=new List<MaterialQuantity>();
        public List<string> PrerequisiteIds=new List<string>();
        public double Cost;
        public double QualityImpact;
        public double Risk;
        public ConstructionPhaseRuntimeData CreateRuntimeData()
        {
            var data=new ConstructionPhaseRuntimeData { RequiresSubcontractor=RequiresSubcontractor,SubcontractorSpecialty=SubcontractorSpecialty,Id=PhaseId,Name=DisplayName,DurationHours=DurationHours,LaborHours=LaborHours,
                CompatibleTrades=new List<WorkerType>(CompatibleTrades),EquipmentRequirements=EquipmentRequirements.ConvertAll(x=>new EquipmentRequirement(x.Type,x.Count)),
                MaterialRequirements=MaterialRequirements.ConvertAll(x=>new MaterialQuantity(x.MaterialId,x.Quantity)),PrerequisiteIds=new List<string>(PrerequisiteIds),
                Cost=Cost,QualityImpact=QualityImpact,Risk=Risk };
            data.Validate(); return data;
        }
        public void Configure(ConstructionPhaseRuntimeData data)
        {
            data.Validate(); var copy=data.Copy(); RequiresSubcontractor=copy.RequiresSubcontractor;SubcontractorSpecialty=copy.SubcontractorSpecialty; PhaseId=copy.Id; DisplayName=copy.Name; DurationHours=copy.DurationHours; LaborHours=copy.LaborHours;
            CompatibleTrades=copy.CompatibleTrades; EquipmentRequirements=copy.EquipmentRequirements; MaterialRequirements=copy.MaterialRequirements;
            PrerequisiteIds=copy.PrerequisiteIds; Cost=copy.Cost; QualityImpact=copy.QualityImpact; Risk=copy.Risk;
        }
    }
}
