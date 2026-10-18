using System;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;

namespace ConstructionEmpire.Data.Bidding
{
    public enum ProjectStatus { Available, Bidding, Won, Lost, Active, Complete }
    public enum QualityTier { Standard, Enhanced, Premium }

    [Serializable, DataContract]
    public struct RequirementProfile
    {
        [DataMember] public float LaborHours;
        [DataMember] public float MaterialUnits;
        [DataMember] public float EquipmentHours;
        [DataMember] public float SubcontractorHours;
        public void Validate()
        {
            BidValidation.NonNegative(LaborHours, nameof(LaborHours));
            BidValidation.NonNegative(MaterialUnits, nameof(MaterialUnits));
            BidValidation.NonNegative(EquipmentHours, nameof(EquipmentHours));
            BidValidation.NonNegative(SubcontractorHours, nameof(SubcontractorHours));
        }
    }

    [Serializable, DataContract]
    public sealed class ProjectData : SaveDataBase
    {
        [DataMember] public ConstructionEmpire.Data.Scheduling.ScheduleData Schedule;
        [DataMember] public ConstructionEmpire.Data.Risk.ProjectSiteProfile Site = new ConstructionEmpire.Data.Risk.ProjectSiteProfile();
        [DataMember] public string OfficeId;
        [DataMember] public string AwardedCompetitorId,AwardedCompetitorName;
        [DataMember] public ConstructionEmpire.Data.Expansion.SiteMapData SiteMap;
        [DataMember] public string Id;
        [DataMember] public string TemplateId;
        [DataMember] public string Title;
        [DataMember] public string BuildingType;
        [DataMember] public string Region;
        [DataMember] public string Location;
        [DataMember] public string ClientId;
        [DataMember] public string ClientName;
        [DataMember] public float Size;
        [DataMember] public float EstimatedDurationHours;
        [DataMember] public float Budget;
        [DataMember] public long DeadlineHour;
        [DataMember] public QualityTier QualityRequirement;
        [DataMember] public RequirementProfile Requirements;
        [DataMember] public float ContractValue;
        [DataMember] public float PenaltyPerLateDay;
        [DataMember] public string PaymentSchedule;
        [DataMember] public ProjectStatus Status;
        [DataMember] public string SubmittedBidId;
        [DataMember] public float SubmittedBidAmount;

        public ProjectData Copy() {var copy=(ProjectData)MemberwiseClone();copy.Schedule=Schedule?.Copy();copy.SiteMap=SiteMap?.Copy();copy.Site=Site?.Copy()??new ConstructionEmpire.Data.Risk.ProjectSiteProfile();return copy;}
        public void Validate()
        {
            BidValidation.Text(Id, nameof(Id)); BidValidation.Text(TemplateId, nameof(TemplateId));
            BidValidation.Text(Title, nameof(Title)); BidValidation.Text(BuildingType, nameof(BuildingType));
            BidValidation.Text(Region, nameof(Region)); BidValidation.Text(Location, nameof(Location));
            BidValidation.Text(ClientId, nameof(ClientId)); BidValidation.Text(ClientName, nameof(ClientName));
            BidValidation.Positive(Size, nameof(Size)); BidValidation.Positive(EstimatedDurationHours, nameof(EstimatedDurationHours));
            BidValidation.Positive(Budget, nameof(Budget)); BidValidation.NonNegative(ContractValue, nameof(ContractValue));
            BidValidation.NonNegative(PenaltyPerLateDay, nameof(PenaltyPerLateDay));
            BidValidation.NonNegative(SubmittedBidAmount, nameof(SubmittedBidAmount));
            if (DeadlineHour < 0 || !Enum.IsDefined(typeof(QualityTier), QualityRequirement) || !Enum.IsDefined(typeof(ProjectStatus), Status))
                throw new ArgumentException("Invalid deadline, quality or status.");
            if (Status == ProjectStatus.Bidding && (string.IsNullOrWhiteSpace(SubmittedBidId) || SubmittedBidAmount <= 0))
                throw new ArgumentException("Bidding project requires a submitted bid.");
            Requirements.Validate(); if(Site==null)Site=new ConstructionEmpire.Data.Risk.ProjectSiteProfile();Site.Validate();
        }
        public override void Migrate()
        {
            if (SchemaVersion != 1) { base.Migrate(); return; }
            Validate(); SchemaVersion = 2;
        }
    }
}
