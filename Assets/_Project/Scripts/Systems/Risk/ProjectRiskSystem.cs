using System;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Risk;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Scheduling;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Risk
{
    /// <summary>Session-owned composition root; all randomness shares the simulation seed and stable entity order.</summary>
    public sealed class ProjectRiskSystem : IRegisterableSystem,IPhaseConditions,IDisposable
    {
        private readonly ManagementSession session;
        public RiskSettings Settings {get;}public WeatherSystem Weather {get;}public SiteConditionsSystem Sites {get;}public RandomEventSystem Events {get;}public ProjectForecastEngine Forecasts {get;}public AlertSystem Alerts {get;}
        public ProjectRiskSystem(ManagementSession session,RiskSettings settings)
        {
            this.session=session;Settings=settings??new RiskSettings();Settings.Validate();
            Weather=new WeatherSystem(session.Projects.Select(x=>x.Region),session.Simulation.Random,Settings);Sites=new SiteConditionsSystem(Settings);Events=new RandomEventSystem(session,Sites,Settings,Assess);
            Alerts=new AlertSystem(Settings,session.Find,()=>session.Workforce.Equipment,()=>session.Workforce.Workers,()=>session.Finance.Obligations,()=>session.Orders);
            Forecasts=new ProjectForecastEngine(CalculateForecast,region=>session.Projects.Where(x=>x.Region==region).Select(x=>x.Id));
            session.Simulation.Construction.Conditions=this;session.Simulation.RegisterBefore(this,session.Simulation.Construction);
        }
        public void Initialize(){foreach(var p in session.Projects)Forecasts.Refresh(p.Id);}
        public ProjectRiskAssessment Assess(ProjectData p)
        {
            var phases=session.Phases(p.Id);var remaining=phases.Where(x=>!x.IsComplete).ToList();var workers=session.Workforce.Workers.Where(x=>x.CurrentProjectId==p.Id).ToList();if(workers.Count==0)workers=session.Expansion?.CrewsFor(p).FirstOrDefault()?.Members??session.Crew.Members;
            double duration=Math.Max(1,remaining.Sum(x=>x.DurationHours*(1-x.Progress/100)));double time=Math.Max(0,p.DeadlineHour-session.Simulation.Time.TotalHours);
            if(p.Schedule!=null && p.Schedule.Phases.Count>0)duration=Math.Max(1,p.Schedule.ForecastCompletion-session.Simulation.Time.TotalHours);
            var required=remaining.SelectMany(x=>x.MaterialRequirements.Select(m=>new {m.MaterialId,Quantity=m.Quantity*(1-x.Progress/100)})).GroupBy(x=>x.MaterialId).ToList();
            double shortage=required.Count==0?0:required.Average(g=>1-Math.Min(1,session.Inventory.GetQuantity(g.Key)/Math.Max(1e-9,g.Sum(x=>x.Quantity))));
            var awards=session.Subcontractors.Awards.Where(x=>x.Quote.ProjectId==p.Id && !x.Completed).ToList();
            var factors=new RiskFactors {SiteDifficulty=p.Site.Difficulty,DeadlineTightness=Clamp(1-(time-duration)/duration),SoilConditions=p.Site.Soil,Remoteness=p.Site.Remoteness,
                SubcontractorReliability=awards.Count==0?0:awards.Average(x=>1-x.Quote.Reliability),MaterialShortageExposure=shortage,DesignComplexity=p.Site.DesignComplexity,WeatherVolatility=Weather.Get(p.Region).Volatility,
                CrewExperience=1-Math.Min(1,workers.Average(x=>x.Experience)/Settings.ExperienceReferenceYears)};
            return ProjectRiskCalculator.Calculate(factors,Settings);
        }
        private ProjectForecast CalculateForecast(string id)
        {
            var p=session.Find(id);if(p==null)return null;long hour=session.Simulation.Time.TotalHours;var phases=session.Phases(id);var assessment=Assess(p);
            double finish=p.Schedule!=null && p.Schedule.Phases.Count>0?p.Schedule.ForecastCompletion:CriticalPathCalculator.Calculate(id,phases,hour).PlannedCompletion;
            var remaining=phases.Where(x=>!x.IsComplete).ToList();
            double weather=remaining.Count==0?1:remaining.Average(x=>Weather.Modifier(p.Region,x.Id));double site=Sites.Modifier(p,Math.Max(hour,Sites.StoppedUntil(id)+1));
            var workers=session.Workforce.Workers.Where(x=>x.CurrentProjectId==id).ToList();double labor=workers.Count==0?1:(double)workers.Count(x=>x.AbsentUntilHour<=hour)/workers.Count;
            var machines=session.Workforce.Equipment.Where(x=>x.CurrentProjectId==id).ToList();double equipment=machines.Count==0?1:machines.Average(x=>x.Status==EquipmentStatus.Broken?0:x.Condition);
            double blocked=Math.Max(0,Sites.StoppedUntil(id)-hour);double rework=remaining.Sum(x=>x.ReworkHoursRemaining);
            return ProjectForecastEngine.Calculate(p,hour,session.Progress(p),session.Cost(p),session.Estimation.Estimate(p,0,0).EstimatedCost,Math.Max(0,finish-hour)+rework,weather*site*labor*equipment,blocked,assessment,Settings);
        }
        public double SiteModifier(ProjectData p,ConstructionPhaseRuntimeData phase,long hour)=>Sites.Modifier(p,hour)*(session.Expansion?.Efficiency(p)??1);
        public double WeatherModifier(ProjectData p,ConstructionPhaseRuntimeData phase)=>Weather.Modifier(p.Region,phase.Id);
        public bool CanComplete(ProjectData p,ConstructionPhaseRuntimeData phase,long hour)=>Events.Inspect(p,phase,hour);
        public void OnTick(TickContext context){Weather.OnTick(context);Events.OnTick(context);}
        public void Dispose(){Forecasts.Dispose();Alerts.Dispose();session.Simulation.Unregister(this);if(ReferenceEquals(session.Simulation.Construction.Conditions,this))session.Simulation.Construction.Conditions=null;}
        private static double Clamp(double x)=>Math.Max(0,Math.Min(1,x));
    }
}
