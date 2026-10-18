using System.Collections.Generic;
using ConstructionEmpire.Data.Construction;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/Management Configuration")]
    public sealed class ManagementConfiguration : ScriptableObject
    {
        public List<StarterProjectAsset> Projects=new List<StarterProjectAsset>();
        public List<EmployeeAsset> Employees=new List<EmployeeAsset>();
        public List<EquipmentAsset> Equipment=new List<EquipmentAsset>();
        public List<CompetitorCompanyAsset> Competitors=new List<CompetitorCompanyAsset>();
        public List<ConstructionPhaseDefinition> DefaultPhases=new List<ConstructionPhaseDefinition>();
        public BiddingSettingsDefinition Bidding;
        public ConstructionEmpire.Data.Finance.FinanceSettings Finance=new ConstructionEmpire.Data.Finance.FinanceSettings();
        public List<MaterialDefinition> Materials=new List<MaterialDefinition>();
        public List<SupplierDefinition> Suppliers=new List<SupplierDefinition>();
        public ConstructionEmpire.Data.Procurement.ProcurementSettings Procurement=new ConstructionEmpire.Data.Procurement.ProcurementSettings();
        public List<SubcontractorCompanyAsset> Subcontractors=new List<SubcontractorCompanyAsset>();
        public ConstructionEmpire.Data.Subcontractors.SubcontractorSettings Subcontracting=new ConstructionEmpire.Data.Subcontractors.SubcontractorSettings();
        public ConstructionEmpire.Data.Risk.RiskSettings Risks=new ConstructionEmpire.Data.Risk.RiskSettings();
        public List<WorldMapRegionAsset> Regions=new List<WorldMapRegionAsset>();
        public ConstructionEmpire.Data.Expansion.ExpansionSettings Expansion=new ConstructionEmpire.Data.Expansion.ExpansionSettings();
        public ConstructionEmpire.Data.Market.MarketSettings Market=new ConstructionEmpire.Data.Market.MarketSettings();
        public List<ResearchUpgradeDefinition> ResearchDefinitions=new List<ResearchUpgradeDefinition>();
        public ConstructionEmpire.Data.Research.ResearchSettings Research=new ConstructionEmpire.Data.Research.ResearchSettings();
        public ConstructionEmpire.Data.Company.DevelopmentSettings Development=new ConstructionEmpire.Data.Company.DevelopmentSettings {StartWithRegularShifts=true};
        public bool EnableProceduralOpportunities=true;
        public ManagementSettings Settings=new ManagementSettings();
    }
}
