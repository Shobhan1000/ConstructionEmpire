using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Data.Research
{
    public enum ResearchEffectKind { EstimationAccuracy, CrewEfficiency, ProcurementDiscount, ReorderThreshold, AutoScheduling, FuelSavings, EquipmentReliability }
    [Serializable] public sealed class ResearchEffectDescriptor
    { public ResearchEffectKind Kind; public double Amount; public ResearchEffectDescriptor Copy()=>(ResearchEffectDescriptor)MemberwiseClone(); }
    [Serializable] public sealed class ResearchUpgradeData
    {
        public string Id,Name,Description,Category;
        public double Cost; public int DurationHours=8,MinimumCompanyLevel=1;
        public List<string> PrerequisiteIds=new List<string>();
        public List<ResearchEffectDescriptor> Effects=new List<ResearchEffectDescriptor>();
        public ResearchUpgradeData Copy(){var c=(ResearchUpgradeData)MemberwiseClone();c.PrerequisiteIds=new List<string>(PrerequisiteIds);c.Effects=Effects.Select(x=>x.Copy()).ToList();return c;}
        public void Validate()
        {
            ConstructionValidation.Text(Id,"Research ID");ConstructionValidation.Text(Name,"Research name");ConstructionValidation.NonNegative(Cost,"Research cost");
            if(Cost>(double)decimal.MaxValue || DurationHours<1 || MinimumCompanyLevel<1 || PrerequisiteIds==null || Effects==null || Effects.Count==0 || PrerequisiteIds.Distinct().Count()!=PrerequisiteIds.Count)throw new ArgumentException("Invalid research definition.");
            foreach(var effect in Effects){if(effect==null || !Enum.IsDefined(typeof(ResearchEffectKind),effect.Kind))throw new ArgumentException("Invalid effect.");ConstructionValidation.Unit(effect.Amount,"Effect amount");}
        }
    }
    [Serializable] public sealed class ResearchSettings
    {
        public int CompanyLevel=1,AutomationEveryHours=6; public double BaseEstimationAccuracy=.7,CashReserve=5000,DailyAutomationBudget=5000;
        public void Validate(){if(CompanyLevel<1 || AutomationEveryHours<1)throw new ArgumentException("Invalid research settings.");ConstructionValidation.Unit(BaseEstimationAccuracy,"Accuracy");ConstructionValidation.NonNegative(CashReserve,"Cash reserve");ConstructionValidation.NonNegative(DailyAutomationBudget,"Automation budget");}
    }
    public readonly struct ResearchChangedEvent
    {public readonly object Owner;public readonly string UpgradeId,Status;public ResearchChangedEvent(object owner,string id,string status){Owner=owner;UpgradeId=id;Status=status;}}
    public readonly struct ResearchAvailableEvent
    {public readonly object Owner;public readonly string UpgradeId;public ResearchAvailableEvent(object owner,string id){Owner=owner;UpgradeId=id;}}
}
