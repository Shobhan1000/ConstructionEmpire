using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Expansion
{
    [Serializable,DataContract]
    public sealed class CompanyOfficeData : SaveDataBase
    {
        [DataMember]public string Id,Name,RegionId,Location;
        [DataMember]public double EstablishmentCost,RentPerDay,PurchaseCost,OperationalEfficiency=1;
        [DataMember]public int ProjectCapacity=3,StaffCapacity=18;
        [DataMember]public bool IsOwned;
        [DataMember]public List<string> EmployeeIds=new List<string>();
        [DataMember]public List<EmployeeData> RegionalWorkforcePool=new List<EmployeeData>();
        public CompanyOfficeData Copy(){var c=(CompanyOfficeData)MemberwiseClone();c.EmployeeIds=new List<string>(EmployeeIds);c.RegionalWorkforcePool=RegionalWorkforcePool.Select(x=>x.Copy()).ToList();return c;}
        public void Validate(){ConstructionValidation.Text(Id,"Office ID");ConstructionValidation.Text(Name,"Office name");ConstructionValidation.Text(RegionId,"Region");foreach(var x in new[]{EstablishmentCost,RentPerDay,PurchaseCost})ConstructionValidation.NonNegative(x,"Office cost");ConstructionValidation.Positive(OperationalEfficiency,"Office efficiency");if(ProjectCapacity<=0 || StaffCapacity<=0 || EmployeeIds==null || RegionalWorkforcePool==null || EmployeeIds.Distinct().Count()!=EmployeeIds.Count || EmployeeIds.Count>StaffCapacity)throw new ArgumentException("Invalid office capacity or workforce.");foreach(var w in RegionalWorkforcePool)w.Validate();}
        public override void Migrate(){if(SchemaVersion!=1){base.Migrate();return;}Validate();SchemaVersion=2;}
    }
    [Serializable]
    public sealed class ExpansionSettings
    {
        [OptionalField] public bool ProceduralOpportunities;
        [OptionalField] public List<OpportunityProfile> OpportunityProfiles=OpportunityProfileCatalog.Create();
        public double OfficeSetupCost=20000,OfficeRentPerDay=150,OfficePurchaseCost=150000,OfficeEfficiency=1,HiringCostPerWorker=1000;
        public int OfficeProjectCapacity=3,OfficeStaffCapacity=18,OpportunityBatchSize=3,MaximumAvailableOpportunitiesPerRegion=10;
        public double RegionalPriceVariation=.15,WorkerTravelKilometresPerHour=40,TransportCostPerKilometre=4,DeliveryKilometresPerHour=30,RemoteOverheadPerKilometrePerDay=2;
        public void Validate(){if(ProceduralOpportunities){if(OpportunityProfiles==null || OpportunityProfiles.Count==0)throw new ArgumentException("Opportunity profiles required.");foreach(var profile in OpportunityProfiles)profile.Validate();}
            foreach(var x in new[]{OfficeSetupCost,OfficeRentPerDay,OfficePurchaseCost,HiringCostPerWorker,TransportCostPerKilometre,RemoteOverheadPerKilometrePerDay})ConstructionValidation.NonNegative(x,"Expansion cost");foreach(var x in new[]{OfficeEfficiency,WorkerTravelKilometresPerHour,DeliveryKilometresPerHour})ConstructionValidation.Positive(x,"Expansion rate");ConstructionValidation.Unit(RegionalPriceVariation,"Price variation");if(OfficeProjectCapacity<=0 || OfficeStaffCapacity<=0 || OpportunityBatchSize<=0 || MaximumAvailableOpportunitiesPerRegion<=0)throw new ArgumentException("Invalid expansion capacity.");}
    }
    public readonly struct ExpansionChangedEvent {public string RegionId {get;}public ExpansionChangedEvent(string id){RegionId=id;}}
    public readonly struct OpportunitiesChangedEvent {public string RegionId {get;}public OpportunitiesChangedEvent(string id){RegionId=id;}}
}
