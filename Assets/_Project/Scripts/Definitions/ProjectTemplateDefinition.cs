using System;
using ConstructionEmpire.Data.Bidding;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/Project Template")]
    public sealed class ProjectTemplateDefinition : ScriptableObject
    {
        public System.Collections.Generic.List<ConstructionPhaseDefinition> Phases=new System.Collections.Generic.List<ConstructionPhaseDefinition>();
        public string TemplateId;
        public string BuildingType;
        public float MinimumSize;
        public float MaximumSize;
        public float ReferenceSize;
        public float BaseDurationHours;
        public RequirementProfile BaseRequirements;
        public QualityTier QualityRequirement;
        public ProjectData CreateProject(string id, string title, string region, string location, string clientId,
            string clientName, float size, float budget, long deadlineHour, float penaltyPerDay, string paymentSchedule)
        {
            BidValidation.Positive(MinimumSize,nameof(MinimumSize)); BidValidation.Positive(MaximumSize,nameof(MaximumSize));
            BidValidation.Positive(ReferenceSize,nameof(ReferenceSize)); BidValidation.Positive(BaseDurationHours,nameof(BaseDurationHours));
            BidValidation.Positive(size,nameof(size)); BaseRequirements.Validate();
            if (MaximumSize<MinimumSize || size<MinimumSize || size>MaximumSize) throw new ArgumentOutOfRangeException(nameof(size));
            float scale=size/ReferenceSize;
            var p=new ProjectData { Id=id,TemplateId=TemplateId,Title=title,BuildingType=BuildingType,Region=region,Location=location,
                ClientId=clientId,ClientName=clientName,Size=size,Budget=budget,DeadlineHour=deadlineHour,
                EstimatedDurationHours=BaseDurationHours*scale,QualityRequirement=QualityRequirement,PenaltyPerLateDay=penaltyPerDay,
                PaymentSchedule=paymentSchedule,Status=ProjectStatus.Available,
                Requirements=new RequirementProfile { LaborHours=BaseRequirements.LaborHours*scale,MaterialUnits=BaseRequirements.MaterialUnits*scale,
                    EquipmentHours=BaseRequirements.EquipmentHours*scale,SubcontractorHours=BaseRequirements.SubcontractorHours*scale } };
            p.Validate(); return p;
        }
    }
}
