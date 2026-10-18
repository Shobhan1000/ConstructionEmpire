using ConstructionEmpire.Data.Save;
using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Scheduling;
using ConstructionEmpire.Systems.Scheduling;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal static class ScheduleScreenPresenter
    {
        private static string Date(double? h)=>h.HasValue ? "D"+((long)Math.Floor(h.Value/24)+1)+" "+(h.Value%24).ToString("00.##")+"h" : "—";
        public static void Build(VisualElement content,MainDashboardController dashboard,ProjectData project)
        {
            var session=dashboard.Session;var phases=session.Phases(project.Id);
            DashboardElements.Label(content,"Original plan and live forecast · highlighted rows are critical · dates use fixed game hours","form-note");
            DashboardElements.Label(content,"","form-note","schedule-summary");
            var rows=DashboardElements.Box(content,"schedule-rows","schedule-rows");
            foreach(var phase in phases)
            {
                var row=DashboardElements.Box(rows,"schedule-row","schedule-"+phase.Id);
                DashboardElements.Label(row,phase.Name,"card-title");DashboardElements.Label(row,"","form-note","dates");
                var track=DashboardElements.Box(row,"gantt-track");var bar=DashboardElements.Box(track,"gantt-bar","bar");DashboardElements.Box(bar,"gantt-progress","fill");
                DashboardElements.Label(row,"","form-note","status");row.tooltip="Prerequisites: "+string.Join(", ",phase.PrerequisiteIds);
                if(phase.RequiresSubcontractor)DashboardElements.Button(row,"Manage specialty quotes",()=>dashboard.Navigate("Subcontractors",project.Id));
            }
            DashboardElements.Label(content,"Reserve a crew and equipment","card-title");
            DashboardElements.Label(content,"Windows use absolute game hours [start, end). Work starts only when prerequisites and resources are ready, and pauses when the window ends. Immediate crew assignments reserve resources until released or complete.","form-note");
            var available=phases.Where(x=>!x.RequiresSubcontractor && !x.IsComplete).ToList();
            var crews=session.Expansion.CrewsFor(project);
            if(available.Count>0 && crews.Count>0)
            {
                var phaseChoice=new DropdownField("Phase",available.Select(x=>x.Name).ToList(),0);phaseChoice.AddToClassList("form-field");content.Add(phaseChoice);
                var crewChoice=new DropdownField("Crew",crews.Select(x=>x.Name).ToList(),0);crewChoice.AddToClassList("form-field");content.Add(crewChoice);
                var start=new LongField("Start hour") {value=session.Simulation.Time.TotalHours+session.Expansion.TravelHours(project)};start.AddToClassList("form-field");content.Add(start);
                var end=new LongField("End hour") {value=session.Simulation.Time.TotalHours+session.Expansion.TravelHours(project)+(long)Math.Ceiling(available[0].DurationHours)};end.AddToClassList("form-field");content.Add(end);
                var selected=new Dictionary<EquipmentRuntimeData,Toggle>();
                foreach(var machine in session.Workforce.Equipment){var toggle=new Toggle(machine.Name+" · "+machine.EquipmentType);content.Add(toggle);selected.Add(machine,toggle);}
                var reserve=DashboardElements.Button(content,"Reserve selected resources",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.ReserveCrew,Id=project.Id,OtherId=available[phaseChoice.index].Id,ThirdId=crews[crewChoice.index].Id,EquipmentIds=selected.Where(x=>x.Value.value).Select(x=>x.Key.Id).ToList(),Start=start.value,End=end.value})),"primary");reserve.SetEnabled(project.Status==ProjectStatus.Active);
                if(project.Status!=ProjectStatus.Active)DashboardElements.Label(content,"Start this project before reserving resources.","form-note");
            }
            DashboardElements.Label(content,"Resource reservations","card-title");DashboardElements.Box(content,"schedule-bookings","schedule-bookings");
            DashboardElements.Label(content,"Material arrivals and subcontractor commitments","card-title");DashboardElements.Box(content,"schedule-deliveries","schedule-deliveries");
            DashboardElements.Box(content,"site-map","site-map");
            Refresh(content,dashboard,project);
        }
        public static void Refresh(VisualElement content,MainDashboardController dashboard,ProjectData project)
        {
            if(content.Q("schedule-rows")==null)return;
            var site=content.Q("site-map");if(site!=null){site.Clear();ExpansionScreenPresenter.Site(site,dashboard,project);}
            var session=dashboard.Session;var phases=session.Phases(project.Id);var plan=project.Schedule!=null && project.Schedule.Phases.Count>0 ? project.Schedule : CriticalPathCalculator.Calculate(project.Id,phases,session.Simulation.Time.TotalHours);
            DashboardElements.Set(content,"schedule-summary","Planned completion "+Date(plan.PlannedCompletion)+" · Forecast "+Date(plan.ForecastCompletion)+" · Shift "+Math.Max(0,plan.ForecastCompletion-plan.PlannedCompletion).ToString("0.##")+" h");
            double span=Math.Max(1,plan.PlannedCompletion-plan.PlannedStart);
            foreach(var task in plan.Phases)
            {
                var row=content.Q("schedule-"+task.PhaseId);if(row==null)continue;
                row.EnableInClassList("schedule-critical",task.IsCriticalPath);row.EnableInClassList("schedule-delayed",task.CurrentDelay>0);
                DashboardElements.Set(row,"dates","Plan "+Date(task.PlannedStart)+" → "+Date(task.PlannedCompletion)+" | Actual "+Date(task.ActualStart)+" → "+Date(task.ActualCompletion));
                DashboardElements.Set(row,"status",$"{task.Progress:0.0}% · Delay {task.CurrentDelay:0.##} h · Float {task.TotalFloat:0.##} h · "+(task.IsCriticalPath ? "Critical path" : "Noncritical"));
                row.Q("bar").style.marginLeft=Length.Percent((float)((task.PlannedStart-plan.PlannedStart)/span*100));row.Q("bar").style.width=Length.Percent((float)((task.PlannedCompletion-task.PlannedStart)/span*100));row.Q("fill").style.width=Length.Percent((float)task.Progress);
            }
            var bookings=content.Q("schedule-bookings");bookings.Clear();
            foreach(var booking in session.Scheduling.Bookings.Where(x=>x.Data.ProjectId==project.Id))
            {
                var d=booking.Data;var row=DashboardElements.Box(bookings,"table-row");row.style.flexWrap=Wrap.Wrap;
                DashboardElements.Label(row,d.PhaseId+" · "+d.ResourceName+" · "+Date(d.StartHour)+" → "+(d.EndHour==long.MaxValue ? "until complete/released" : Date(d.EndHour))+" · "+d.Status+" "+d.Conflict,"cell");
                if(booking.Open && booking.Source is ConstructionEmpire.Systems.Construction.InternalCrewWorkSource)DashboardElements.Button(row,"Release reservation",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.CancelBooking,Id=d.ProjectId,OtherId=d.Id})));
            }
            if(bookings.childCount==0)DashboardElements.Label(bookings,"No resource reservations.","form-note");
            var commitments=content.Q("schedule-deliveries");commitments.Clear();
            foreach(var order in session.Orders.Where(x=>x.ProjectId==project.Id && !x.Delivered))DashboardElements.Label(commitments,order.MaterialId+" · due "+Date(order.DueHour)+" · "+order.Status,"form-note");
            foreach(var award in session.Subcontractors.Awards.Where(x=>x.Quote.ProjectId==project.Id))DashboardElements.Label(commitments,award.Quote.PhaseId+" · "+(award.Completed ? "Complete" : "Available after "+Date(award.AvailableFromHour))+" · quoted "+award.Quote.DurationHours+" h","form-note");
            DashboardElements.Button(commitments,"Schedule material orders",()=>dashboard.Navigate("Materials",project.Id));DashboardElements.Button(commitments,"Schedule subcontracted work",()=>dashboard.Navigate("Subcontractors",project.Id));
        }
    }
}
