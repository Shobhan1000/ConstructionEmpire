using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Expansion;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Data.Procurement;
using ConstructionEmpire.Data.Research;
using ConstructionEmpire.Data.Risk;
using ConstructionEmpire.Data.Subcontractors;
using ConstructionEmpire.Data.Time;
namespace ConstructionEmpire.Data.Save
{
    /// <summary>Frozen rules and starting data for deterministic restoration; no Unity object references.</summary>
    [DataContract]
    public sealed class SessionStartData
    {
        [DataMember]public ConstructionEmpire.Data.Company.DevelopmentSettings Development;
        [DataMember]public CompanyData Company;
        [DataMember]public GameTimeData Clock;
        [DataMember]public int Seed;
        [DataMember]public CrewEfficiencyConfiguration CrewEfficiency;
        [DataMember]public List<ProjectData> Projects;
        [DataMember]public StarterWorkforce Workforce;
        [DataMember]public Dictionary<string,List<ConstructionPhaseRuntimeData>> Phases;
        [DataMember]public EstimationRates Rates;
        [DataMember]public BiddingConfiguration Bidding;
        [DataMember]public List<CompetitorCompanyData> Competitors;
        [DataMember]public ManagementSettings Management;
        [DataMember]public FinanceSettings Finance;
        [DataMember]public List<SupplierData> Suppliers;
        [DataMember]public ProcurementSettings Procurement;
        [DataMember]public List<SubcontractorCompanyData> Subcontractors;
        [DataMember]public SubcontractorSettings Subcontracting;
        [DataMember]public RiskSettings Risks;
        [DataMember]public List<WorldMapData> Regions;
        [DataMember]public ExpansionSettings Expansion;
        [DataMember]public MarketSettings Market;
        [DataMember]public List<ResearchUpgradeData> Research;
        [DataMember]public ResearchSettings ResearchSettings;
    }
}
