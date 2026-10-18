using System;
using System.Linq;
using ConstructionEmpire.Data.Save;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal static class DevelopmentScreenPresenter
    {
        public static void Company(VisualElement root,MainDashboardController dashboard)
        {
            var d=dashboard.Session.Development;
            DashboardElements.Label(root,"Level "+d.Level+" · "+d.LevelName,"card-title");
            DashboardElements.Label(root,d.CompletedProjects+" completed projects · Estimated net asset value "+DashboardElements.Money(d.Valuation),"form-note");
            if(d.Level<d.Settings.Levels.Count){var next=d.Settings.Levels[d.Level];DashboardElements.Label(root,"Next: "+next.Name+" · "+next.CompletedProjects+" completed projects, "+DashboardElements.Money(next.EarnedRevenue)+" earned revenue and "+next.Reputation+" reputation.","form-note");}
            DashboardElements.Label(root,"Long-term objectives","card-title");
            Goal(root,"Complete projects",d.CompletedProjects,d.Settings.GoalCompletedProjects);
            Goal(root,"Net asset value",d.Valuation,d.Settings.GoalValuation);
            Goal(root,"Reputation",dashboard.Bootstrap.Company.Reputation,d.Settings.GoalReputation);
            Goal(root,"Operate in every region",dashboard.Session.Expansion.Offices.Count,dashboard.Session.Expansion.Regions.Count);
            DashboardElements.Label(root,"Continue in sandbox after reaching your goals; promotions are retained if finances later weaken.","form-note");
        }
        private static void Goal(VisualElement root,string name,double current,double target)
        {var bar=new ProgressBar {title=name+" · "+current.ToString("N0")+" / "+target.ToString("N0"),value=(float)Math.Min(100,current/target*100)};root.Add(bar);}
        public static void Clients(VisualElement root,MainDashboardController dashboard)
        {
            foreach(var c in dashboard.Session.Development.Clients)
            {var row=DashboardElements.Box(root,"table-row");DashboardElements.Label(row,c.Name,"project-name-cell");DashboardElements.Label(row,c.Preference,"cell");DashboardElements.Label(row,"Relationship "+c.Relationship.ToString("P0"),"cell");DashboardElements.Label(row,c.CompletedProjects+" completed","cell");row.tooltip="On-time completion builds trust; delays and poor quality reduce it. Relationship contributes to future bid awards.";}
        }
        public static void EquipmentCatalog(VisualElement root,MainDashboardController dashboard)
        {
            DashboardElements.Label(root,"Acquire equipment","card-title");
            foreach(var e in dashboard.Session.Development.Settings.EquipmentCatalog)
            {
                var row=DashboardElements.Box(root,"table-row");DashboardElements.Label(row,e.Name,"project-name-cell");
                DashboardElements.Button(row,"Buy · "+DashboardElements.Money(e.PurchasePrice),()=>dashboard.AskConfirmation("Buy "+e.Name+"? This spends cash; depreciation is recorded as the machine works.",()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.AcquireEquipment,Id=e.DefinitionId})));
                DashboardElements.Button(row,"Rent · "+DashboardElements.Money(e.RentalPricePerDay)+"/day",()=>dashboard.AskConfirmation("Rent "+e.Name+"? Daily charges continue, including idle days, until returned.",()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.AcquireEquipment,Id=e.DefinitionId,Flag=true})));
            }
        }
        public static void CrewPolicy(VisualElement root,MainDashboardController dashboard,ConstructionEmpire.Data.Construction.CrewData crew)
        {
            var overtime=new Toggle("Overtime shift (higher wages)"){value=crew.OvertimeMultiplier>1};var weekends=new Toggle("Work weekends"){value=crew.WorkWeekends};root.Add(overtime);root.Add(weekends);
            DashboardElements.Label(root,crew.UseShiftPolicy?"Shift "+crew.ShiftStartHour+":00 for "+crew.ShiftHours+" hours":"Continuous coverage — choose a shift below to give workers regular rest","form-note");
            DashboardElements.Button(root,"Apply working hours",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.SetWorkPolicy,Id=crew.Id,Flag=overtime.value,OtherFlag=weekends.value})));
        }
        public static void Settings(VisualElement root,MainDashboardController dashboard)
        {
            DashboardElements.Label(root,"Game and display","card-title");
            DashboardElements.Label(root,"Open Game menu to save, load, recover a backup, or start a new company. Loaded games begin paused.","form-note");
            var scale=new SliderInt("Interface size (%)",85,150){value=dashboard.InterfaceScalePercent};root.Add(scale);scale.RegisterValueChangedCallback(e=>dashboard.SetInterfaceScale(e.newValue));scale.tooltip="Increase text and control size. Smaller windows use stacked layouts and scrolling.";
            var fullscreen=new Toggle("Fullscreen"){value=Screen.fullScreen};fullscreen.RegisterValueChangedCallback(e=>Screen.fullScreen=e.newValue);root.Add(fullscreen);
            DashboardElements.Button(root,"1280 × 720 window",()=>Screen.SetResolution(1280,720,false));DashboardElements.Button(root,"1920 × 1080 window",()=>Screen.SetResolution(1920,1080,false));
            DashboardElements.Label(root,"Use Tab to move between controls and Enter to activate buttons. Tooltips explain financial and scheduling metrics. Simulation speed controls are in the top bar.","form-note");
        }
    }
}
