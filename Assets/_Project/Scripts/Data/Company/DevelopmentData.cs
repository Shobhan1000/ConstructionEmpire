using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Data.Company
{
    [Serializable]public sealed class CompanyLevelDefinition
    {public string Name;public int CompletedProjects;public double EarnedRevenue,Reputation;}
    [Serializable]public sealed class ClientRelationshipData
    {public string Id,Name;public double Relationship=.5;public int CompletedProjects;public string Preference;}
    [Serializable]public sealed class DevelopmentSettings
    {
        public List<CompanyLevelDefinition> Levels=new List<CompanyLevelDefinition>{
            new CompanyLevelDefinition{Name="Small Contractor"},
            new CompanyLevelDefinition{Name="Local Contractor",CompletedProjects=3,EarnedRevenue=100000,Reputation=50},
            new CompanyLevelDefinition{Name="Regional Contractor",CompletedProjects=10,EarnedRevenue=500000,Reputation=60},
            new CompanyLevelDefinition{Name="Major Contractor",CompletedProjects=25,EarnedRevenue=2000000,Reputation=70},
            new CompanyLevelDefinition{Name="Construction Corporation",CompletedProjects=60,EarnedRevenue=10000000,Reputation=80}};
        public double SuccessfulRelationshipGain=.08,DelayedRelationshipPenalty=.06,PoorQualityPenalty=.1,RepeatClientThreshold=.7;
        public int GoalCompletedProjects=100;public double GoalValuation=10000000,GoalReputation=95;
        public double ChangeOrderScopeFraction=.2,ChangeOrderMarkup=.25,NegotiationIncrease=.1,NegotiationBaseAcceptance=.25;
        public double ResaleValueFraction=.6,OvertimeWageMultiplier=1.5;
        public bool StartWithRegularShifts;
        public int StandardShiftHours=8,OvertimeShiftHours=12,ShiftStartHour=8;
        public List<EquipmentRuntimeData> EquipmentCatalog=CreateEquipmentCatalog();
        private static List<EquipmentRuntimeData> CreateEquipmentCatalog()
        {
            var list=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment().Equipment.Select(x=>x.Copy()).ToList();
            foreach(EquipmentType type in Enum.GetValues(typeof(EquipmentType)))
            {if(list.Any(x=>x.EquipmentType==type))continue;list.Add(new EquipmentRuntimeData {Id="catalog-"+type,DefinitionId="catalog-"+type,Name=type.ToString(),EquipmentType=type,Condition=1,Location="Depot",FuelCapacity=100,CurrentFuel=100,FuelPerHour=2,CostPerHour=15,ProductivityMultiplier=1,PurchasePrice=80000,RentalPricePerDay=250,MaintenanceCost=1200});}
            return list;
        }
        public void Validate()
        {
            if(Levels==null || Levels.Count!=5 || EquipmentCatalog==null || EquipmentCatalog.Count==0)throw new ArgumentException("Configure five company levels and equipment.");
            foreach(var l in Levels){if(l==null || string.IsNullOrWhiteSpace(l.Name) || l.CompletedProjects<0)throw new ArgumentException("Invalid company level.");ConstructionValidation.NonNegative(l.EarnedRevenue,"Level revenue");ConstructionValidation.Percent(l.Reputation,"Level reputation");}
            foreach(var e in EquipmentCatalog)e.Validate();
            foreach(var x in new[]{SuccessfulRelationshipGain,DelayedRelationshipPenalty,PoorQualityPenalty,RepeatClientThreshold,NegotiationBaseAcceptance})ConstructionValidation.Unit(x,"Relationship factor");
            ConstructionValidation.Positive(ChangeOrderScopeFraction,"Scope fraction");ConstructionValidation.NonNegative(ChangeOrderMarkup,"Scope markup");ConstructionValidation.NonNegative(NegotiationIncrease,"Negotiation increase");
            if(GoalCompletedProjects<1)throw new ArgumentException("Invalid long-term goal.");ConstructionValidation.Positive(GoalValuation,"Valuation goal");ConstructionValidation.Percent(GoalReputation,"Reputation goal");
            ConstructionValidation.Unit(ResaleValueFraction,"Resale fraction");ConstructionValidation.Positive(OvertimeWageMultiplier,"Overtime wage");
            if(StandardShiftHours<1 || OvertimeShiftHours<StandardShiftHours || ShiftStartHour<0 || ShiftStartHour+OvertimeShiftHours>24)throw new ArgumentException("Invalid shift hours.");
        }
    }
    public readonly struct CompanyDevelopmentChangedEvent
    {public readonly object Source;public readonly string Message;public CompanyDevelopmentChangedEvent(object source,string message){Source=source;Message=message;}}
}
