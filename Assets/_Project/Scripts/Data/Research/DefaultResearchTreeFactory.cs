using System.Collections.Generic;
namespace ConstructionEmpire.Data.Research
{
    public static class DefaultResearchTreeFactory
    {
        public static List<ResearchUpgradeData> CreateStarterTree()=>new List<ResearchUpgradeData>
        {
            Make("takeoffs","Digital Takeoffs","Project Management","Improve estimation confidence with standardized digital quantity takeoffs.",1500,8,ResearchEffectKind.EstimationAccuracy,.15),
            Make("crew-planning","Coordinated Crews","Construction","Raise crew efficiency through shared task plans and standardized working methods.",2500,12,ResearchEffectKind.CrewEfficiency,.08,"takeoffs"),
            Make("supplier-negotiation","Preferred Purchasing","Materials","Negotiate a company-wide material discount alongside existing bulk tiers.",2500,12,ResearchEffectKind.ProcurementDiscount,.05,"takeoffs"),
            Make("reordering","Stock Watch","Automation","Enable bounded automatic reordering when inventory and incoming orders fall below remaining demand thresholds.",4000,16,ResearchEffectKind.ReorderThreshold,.25,"supplier-negotiation"),
            Make("scheduling","Dispatch Planner","Automation","Assign available regional crews to eligible tasks while respecting company-wide booking conflicts.",4500,16,ResearchEffectKind.AutoScheduling,1,"crew-planning"),
            Make("fuel","Efficient Fleet","Equipment","Reduce equipment fuel consumption through improved operating procedures.",3000,12,ResearchEffectKind.FuelSavings,.15,"crew-planning"),
            Make("reliability","Preventive Maintenance","Equipment","Reduce condition-related equipment breakdown probability with systematic maintenance planning.",4000,16,ResearchEffectKind.EquipmentReliability,.2,"fuel"),
            Make("ai-estimating","Assisted Estimating","Administration","Narrow estimate uncertainty further using accumulated planning expertise.",6000,24,ResearchEffectKind.EstimationAccuracy,.15,"takeoffs","scheduling")
        };
        private static ResearchUpgradeData Make(string id,string name,string category,string description,double cost,int hours,ResearchEffectKind kind,double amount,params string[] prerequisites)=>new ResearchUpgradeData {Id=id,Name=name,Category=category,Description=description,Cost=cost,DurationHours=hours,MinimumCompanyLevel=id=="ai-estimating"?2:1,PrerequisiteIds=new List<string>(prerequisites),Effects=new List<ResearchEffectDescriptor>{new ResearchEffectDescriptor {Kind=kind,Amount=amount}}};
    }
}
