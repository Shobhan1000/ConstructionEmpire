using ConstructionEmpire.Data.Save;
using System.Linq;
using ConstructionEmpire.Data.Research;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal static class ResearchScreenPresenter
    {
        private static string EffectText(ResearchEffectDescriptor effect)
        {
            string value=effect.Amount.ToString("P0");
            switch(effect.Kind)
            {
                case ResearchEffectKind.EstimationAccuracy:return "Estimate confidence +"+value;
                case ResearchEffectKind.CrewEfficiency:return "Crew efficiency +"+value;
                case ResearchEffectKind.ProcurementDiscount:return "Material purchase discount "+value;
                case ResearchEffectKind.ReorderThreshold:return "Reorder below "+value+" of remaining requirements";
                case ResearchEffectKind.AutoScheduling:return "Automatic crew dispatch available";
                case ResearchEffectKind.FuelSavings:return "Fuel consumption −"+value;
                case ResearchEffectKind.EquipmentReliability:return "Breakdown probability −"+value;
                default:return "Research modifier";
            }
        }
        public static void Build(VisualElement content,MainDashboardController dashboard)
        {
            var session=dashboard.Session;var integration=session.Research;var research=integration.System;
            DashboardElements.Label(content,"Company level "+research.CompanyLevel+" · One research project at a time · Paid when started","form-note");
            if(research.ActiveId!=null)DashboardElements.Label(content,"Research in progress · "+System.Math.Max(0,research.CompletionHour-session.Simulation.Time.TotalHours)+" hours remaining","card-title");
            var procurement=new Toggle("Enable automatic material reordering") {value=integration.ProcurementEnabled};procurement.SetEnabled(research.Modifier(ResearchEffectKind.ReorderThreshold)>0);procurement.RegisterValueChangedCallback(e=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.SetAutomation,Flag=e.newValue,OtherFlag=integration.SchedulingEnabled})));content.Add(procurement);
            var scheduling=new Toggle("Enable automatic crew scheduling") {value=integration.SchedulingEnabled};scheduling.SetEnabled(research.Modifier(ResearchEffectKind.AutoScheduling)>0);scheduling.RegisterValueChangedCallback(e=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.SetAutomation,Flag=integration.ProcurementEnabled,OtherFlag=e.newValue})));content.Add(scheduling);
            DashboardElements.Label(content,integration.AutomationStatus,"form-note");
            foreach(var upgrade in research.Upgrades)
            {
                var card=DashboardElements.Box(content,"panel","research-"+upgrade.Id);DashboardElements.Label(card,upgrade.Name+" · "+upgrade.Category,"card-title");DashboardElements.Label(card,upgrade.Description,"form-note");
                DashboardElements.Label(card,DashboardElements.Money(upgrade.Cost)+" · "+upgrade.DurationHours+" hours · Company level "+upgrade.MinimumCompanyLevel,"form-note");
                if(upgrade.PrerequisiteIds.Count>0)DashboardElements.Label(card,"Requires: "+string.Join(", ",upgrade.PrerequisiteIds.Select(id=>research.Upgrades.Single(x=>x.Id==id).Name)),"form-note");
                DashboardElements.Label(card,string.Join(" · ",upgrade.Effects.Select(x=>EffectText(x))),"form-note");
                string reason=research.BlockReason(upgrade.Id);var button=DashboardElements.Button(card,reason??"Start research",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.StartResearch,Id=upgrade.Id})),"primary");button.SetEnabled(reason==null);
            }
        }
    }
}
