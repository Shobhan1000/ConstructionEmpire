using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Scheduling;
using ConstructionEmpire.Systems.Construction;
namespace ConstructionEmpire.Systems.Scheduling
{
    public static class CriticalPathCalculator
    {
        private const double Epsilon=1e-8;
        public static ScheduleData Calculate(string projectId,IEnumerable<ConstructionPhaseRuntimeData> source,double startHour=0)
        {
            if(string.IsNullOrWhiteSpace(projectId) || double.IsNaN(startHour) || double.IsInfinity(startHour) || startHour<0)throw new ArgumentException("Valid project and start hour required.");
            var phases=source.ToList();var graph=new PhaseDependencyGraph(phases);var byId=phases.ToDictionary(x=>x.Id);var rows=new Dictionary<string,PhaseScheduleData>();
            foreach(var id in graph.TopologicalOrder)
            {
                var p=byId[id];double start=p.PrerequisiteIds.Count==0 ? startHour : p.PrerequisiteIds.Max(x=>rows[x].PlannedCompletion);
                rows[id]=new PhaseScheduleData {PhaseId=id,Name=p.Name,PlannedStart=start,PlannedCompletion=start+p.DurationHours,ForecastStart=start,ForecastCompletion=start+p.DurationHours};
            }
            var result=new ScheduleData {ProjectId=projectId,PlannedStart=startHour,PlannedCompletion=rows.Values.Max(x=>x.PlannedCompletion),Phases=graph.TopologicalOrder.Select(x=>rows[x]).ToList()};
            result.ForecastCompletion=result.PlannedCompletion;SetCritical(phases,graph,rows,result.ForecastCompletion);return result;
        }
        public static void Recalculate(ScheduleData schedule,IEnumerable<ConstructionPhaseRuntimeData> source,double now)
        {
            var phases=source.ToList();var graph=new PhaseDependencyGraph(phases);var byId=phases.ToDictionary(x=>x.Id);var rows=schedule.Phases.ToDictionary(x=>x.PhaseId);
            foreach(var id in graph.TopologicalOrder)
            {
                var p=byId[id];var row=rows[id];row.Progress=p.Progress;
                if(p.IsComplete){row.ForecastStart=row.ActualStart??row.PlannedStart;row.ForecastCompletion=row.ActualCompletion??now;}
                else
                {
                    double available=Math.Max(now,row.PlannedStart);
                    if(p.PrerequisiteIds.Count>0)available=Math.Max(available,p.PrerequisiteIds.Max(x=>rows[x].ForecastCompletion));
                    row.ForecastStart=available;row.ForecastCompletion=available+p.DurationHours*(1-p.Progress/100);
                }
                row.CurrentDelay=Math.Max(0,row.ForecastCompletion-row.PlannedCompletion);
            }
            schedule.ForecastCompletion=rows.Values.Max(x=>x.ForecastCompletion);SetCritical(phases,graph,rows,schedule.ForecastCompletion);
        }
        private static void SetCritical(List<ConstructionPhaseRuntimeData> phases,PhaseDependencyGraph graph,Dictionary<string,PhaseScheduleData> rows,double finish)
        {
            var latest=new Dictionary<string,double>();
            foreach(var id in graph.TopologicalOrder.Reverse())
            {
                var successors=phases.Where(x=>x.PrerequisiteIds.Contains(id)).Select(x=>x.Id).ToList();
                double end=successors.Count==0 ? finish : successors.Min(x=>latest[x]);var row=rows[id];
                latest[id]=end-(row.ForecastCompletion-row.ForecastStart);row.TotalFloat=Math.Max(0,end-row.ForecastCompletion);row.IsCriticalPath=row.TotalFloat<=Epsilon;
            }
        }
    }
}
