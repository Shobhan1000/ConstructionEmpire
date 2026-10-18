using ConstructionEmpire.Data.Save;
using System;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Risk;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal static class RiskScreenPresenter
    {
        public static void Alerts(VisualElement parent,MainDashboardController dashboard,string projectId=null)
        {
            if(parent==null)return;parent.Clear();var alerts=dashboard.Session.Risks.Alerts.Active.Where(x=>projectId==null || x.ProjectId==projectId).ToList();
            if(alerts.Count==0)DashboardElements.Label(parent,"No proactive warnings. Forecast thresholds are being monitored.","empty-state");
            foreach(var alert in alerts)
            {
                var row=DashboardElements.Box(parent,"notification");row.AddToClassList(alert.Critical?"critical":"warning");
                DashboardElements.Label(row,alert.Title,"notification-title");DashboardElements.Label(row,alert.Description,"notification-description");
                DashboardElements.Button(row,"Review",()=>dashboard.Navigate(alert.Screen,alert.ProjectId));
            }
        }
        public static void Project(VisualElement parent,MainDashboardController dashboard,ProjectData project)
        {
            var session=dashboard.Session;var risks=session.Risks;var f=risks.Forecasts.Get(project.Id);if(f==null)return;
            DashboardElements.Label(parent,"Risk & forecast","card-title");
            DashboardElements.Label(parent,$"{f.Risk.Level} Risk · score {f.Risk.Score:P0} · {risks.Weather.Get(project.Region).Weather} in {project.Region}","form-note");
            DashboardElements.Label(parent,$"Expected completion: {Date(f.ExpectedCompletionHour)} · Contract deadline: {Date(f.DeadlineHour)} · {(f.DelayHours>0?f.DelayHours.ToString("0")+" hours late":"On track")}","table-row");
            DashboardElements.Label(parent,$"Expected final cost {DashboardElements.Money(f.ExpectedFinalCost)} · Budget {DashboardElements.Money(f.Budget)} · Expected profit {DashboardElements.Money(f.ExpectedProfit)}","table-row");
            DashboardElements.Label(parent,"Forecast is an estimate using remaining work, current conditions and a tunable risk allowance; it does not guarantee an outcome.","footnote");
            foreach(var factor in f.Risk.Contributions)DashboardElements.Label(parent,$"{factor.Key}: {factor.Value:P1} of total risk","form-note");
            DashboardElements.Label(parent,$"Site: {project.Site.Description} · Quality {project.Site.Quality:P0} · Training {project.Site.SafetyTraining:P0}","form-note");
            var actions=DashboardElements.Box(parent,"action-row");var train=DashboardElements.Button(actions,"Improve crew safety training",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.TrainCrew,Id=project.Id})));
            train.SetEnabled(project.Status==ProjectStatus.Active && session.Workforce.Workers.Any(x=>x.CurrentProjectId==project.Id));train.tooltip=DashboardElements.Money(risks.Settings.TrainingCostPerWorker)+" per assigned worker";
            DashboardElements.Button(actions,"Review equipment",()=>dashboard.Navigate("Equipment",project.Id));DashboardElements.Button(actions,"Review crew / rest",()=>dashboard.Navigate("Project Detail",project.Id));
            var alerts=DashboardElements.Box(parent,"risk-alerts");Alerts(alerts,dashboard,project.Id);
            DashboardElements.Label(parent,"Recorded events","card-title");
        }
        public static string Date(double hour)=>"Day "+((long)Math.Floor(hour/24)+1)+", "+((int)Math.Floor(hour)%24).ToString("00")+":00";
    }
}
