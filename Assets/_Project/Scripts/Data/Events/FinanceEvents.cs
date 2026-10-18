using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Finance;
namespace ConstructionEmpire.Data.Events
{
    public readonly struct FinanceChangedEvent
    { public CompanyData Company {get;} public string ProjectId {get;} public FinanceChangedEvent(CompanyData company,string projectId=null) { Company=company;ProjectId=projectId; } }
    public readonly struct CashFlowWarningEvent
    { public CompanyData Company {get;} public decimal Cash {get;} public bool Critical {get;} public CashFlowWarningEvent(CompanyData company,decimal cash,bool critical) {Company=company;Cash=cash;Critical=critical;} }
    public readonly struct ConstructionWorkAccruedEvent
    {
        public string ProjectId {get;} public double LaborCost {get;} public double EquipmentOperatingCost {get;} public double PhaseCost {get;} public double HoursWorked {get;}
        public System.Collections.Generic.IReadOnlyList<ConstructionEmpire.Data.Construction.EquipmentRuntimeData> Equipment {get;}
        public ConstructionWorkAccruedEvent(string id,double labor,double equipment,double phaseCost,double hours,System.Collections.Generic.IReadOnlyList<ConstructionEmpire.Data.Construction.EquipmentRuntimeData> machines=null) { Equipment=machines; ProjectId=id;LaborCost=labor;EquipmentOperatingCost=equipment;PhaseCost=phaseCost;HoursWorked=hours; }
    }
    public readonly struct ProfitabilityWarningEvent
    { public string ProjectId {get;} public decimal ForecastCost {get;} public decimal ContractValue {get;} public ProfitabilityWarningEvent(string id,decimal cost,decimal value) { ProjectId=id;ForecastCost=cost;ContractValue=value; } }
}
