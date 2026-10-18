using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Risk;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Systems.Risk
{
    public sealed class ProjectForecastEngine : IDisposable
    {
        private readonly EventSubscriptions subscriptions=new EventSubscriptions();private readonly Dictionary<string,ProjectForecast> forecasts=new Dictionary<string,ProjectForecast>();
        private readonly Func<string,ProjectForecast> calculate;
        public ProjectForecastEngine(Func<string,ProjectForecast> calculate,Func<string,IEnumerable<string>> projectsInRegion)
        {
            this.calculate=calculate??throw new ArgumentNullException(nameof(calculate));
            subscriptions.Subscribe<ConstructionEmpire.Data.Scheduling.ScheduleChangedEvent>(e=>Refresh(e.ProjectId));
            subscriptions.Subscribe<FinanceChangedEvent>(e=>{if(e.ProjectId!=null)Refresh(e.ProjectId);});
            subscriptions.Subscribe<PhaseProgressChangedEvent>(e=>Refresh(e.ProjectId));
            subscriptions.Subscribe<ProjectStartedEvent>(e=>Refresh(e.ProjectId));subscriptions.Subscribe<ProjectCompletedEvent>(e=>Refresh(e.ProjectId));
            subscriptions.Subscribe<ResourcesChangedEvent>(e=>{if(e.ProjectId!=null)Refresh(e.ProjectId);else foreach(var id in forecasts.Keys.ToArray())Refresh(id);});
            subscriptions.Subscribe<RiskStateChangedEvent>(e=>Refresh(e.ProjectId));
            subscriptions.Subscribe<WeatherChangedEvent>(e=>{foreach(var id in projectsInRegion(e.Region))Refresh(id);});
            subscriptions.Subscribe<MaterialDeliveredEvent>(e=>{foreach(var id in forecasts.Keys.ToArray())Refresh(id);});subscriptions.Subscribe<MaterialDelayedEvent>(e=>Refresh(e.ProjectId));
            subscriptions.Subscribe<ConstructionEmpire.Data.Procurement.ProcurementChangedEvent>(e=>Refresh(e.ProjectId));
            subscriptions.Subscribe<ConstructionEmpire.Data.Subcontractors.SubcontractorChangedEvent>(e=>Refresh(e.ProjectId));
        }
        public ProjectForecast Get(string id)=>forecasts.TryGetValue(id,out var forecast)?forecast:null;
        public void Refresh(string id){if(id==null)return;var forecast=calculate(id);if(forecast==null)return;forecasts[id]=forecast;GameEvents.Publish(new ForecastChangedEvent(forecast));}
        public static ProjectForecast Calculate(ProjectData project,long hour,double progress,double incurredCost,double baselineCost,double remainingCriticalHours,double productivity,double blockedHours,ProjectRiskAssessment risk,RiskSettings settings)
        {
            if(project==null || risk==null || settings==null)throw new ArgumentNullException();settings.Validate();ConstructionValidation.Percent(progress,"Progress");
            foreach(var x in new[]{incurredCost,baselineCost,remainingCriticalHours,productivity,blockedHours})ConstructionValidation.NonNegative(x,"Forecast input");
            // Incurred costs are retained in full. Only unperformed baseline work gets the risk allowance;
            // upfront material purchases are never extrapolated as a repeating hourly run rate.
            double remaining=baselineCost*(1-progress/100);double cost=incurredCost+remaining*(1+risk.Score*settings.ForecastRiskCostAllowance);
            bool complete=project.Status==ProjectStatus.Complete || progress>=100;
            double completion=complete ? project.Schedule?.ForecastCompletion??hour : hour+blockedHours+remainingCriticalHours/Math.Max(settings.MinimumForecastProductivity,productivity)*(1+risk.Score*settings.ForecastRiskScheduleAllowance);
            return new ProjectForecast {ProjectId=project.Id,UpdatedHour=hour,Progress=progress,Risk=risk,ExpectedFinalCost=complete?incurredCost:cost,ExpectedCompletionHour=completion,DeadlineHour=project.DeadlineHour,Budget=project.Budget,ExpectedProfit=project.ContractValue-(complete?incurredCost:cost)};
        }
        public void Dispose()=>subscriptions.Dispose();
    }
}
