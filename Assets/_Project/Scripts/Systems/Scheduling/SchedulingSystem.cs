using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Scheduling;
using ConstructionEmpire.Systems.Construction;
namespace ConstructionEmpire.Systems.Scheduling
{
    public sealed class ScheduledAssignment
    {
        public ResourceBookingData Data {get;}public IPhaseWorkSource Source {get;}public IReadOnlyList<EquipmentRuntimeData> Equipment {get;}public IReadOnlyList<string> ResourceIds {get;}
        internal ScheduledAssignment(ResourceBookingData data,IPhaseWorkSource source,List<EquipmentRuntimeData> equipment,List<string> ids){Data=data;Source=source;Equipment=equipment.AsReadOnly();ResourceIds=ids.AsReadOnly();}
        public bool Open=>Data.Status==BookingStatus.Pending || Data.Status==BookingStatus.Active || Data.Status==BookingStatus.Blocked;
    }
    public sealed class SchedulingSystem
    {
        private sealed class Plan {public ProjectData Project;public List<ConstructionPhaseRuntimeData> Phases;public HashSet<string> Reported=new HashSet<string>();}
        private readonly Dictionary<string,Plan> plans=new Dictionary<string,Plan>();private readonly List<ScheduledAssignment> bookings=new List<ScheduledAssignment>();
        public long CurrentHour {get;private set;}public IReadOnlyList<ScheduledAssignment> Bookings=>bookings.AsReadOnly();
        public void Track(ProjectData project,IEnumerable<ConstructionPhaseRuntimeData> source,long hour)
        {var phases=source.ToList();if(plans.ContainsKey(project.Id))throw new InvalidOperationException("Project ID is already owned by an execution engine.");if(plans.Values.Any(p=>p.Phases.Any(old=>phases.Any(next=>ReferenceEquals(old,next)))))throw new InvalidOperationException("Runtime phase instances cannot be shared between projects. Copy template data first.");project.Schedule=CriticalPathCalculator.Calculate(project.Id,phases,hour);plans[project.Id]=new Plan {Project=project,Phases=phases};CurrentHour=hour;}
        public ScheduleData Get(string project)=>plans[project].Project.Schedule;
        public ScheduledAssignment Reserve(string project,string phase,IPhaseWorkSource source,IEnumerable<EquipmentRuntimeData> machines,long start,long end)
        {
            if(!plans.TryGetValue(project,out var plan) || plan.Project.Status!=ProjectStatus.Active)throw new InvalidOperationException("Schedule resources on an active project.");
            var task=plan.Phases.Single(x=>x.Id==phase);source.Validate();if(task.IsComplete || !source.CanPerform(task))throw new InvalidOperationException("Choose an unfinished compatible phase.");
            if(start<CurrentHour || end<=start)throw new ArgumentException("Booking requires a future or current start and a later end.");
            var equipment=machines.ToList();foreach(var m in equipment)m.Validate();
            if(equipment.Select(x=>x.Id).Distinct().Count()!=equipment.Count)throw new ArgumentException("Duplicate equipment.");
            var ids=source.WorkerIds.Select(x=>"worker:"+x).Concat(equipment.Select(x=>"equipment:"+x.Id)).Append("provider:"+source.Id).ToList();
            var conflict=bookings.FirstOrDefault(x=>x.Open && start<x.Data.EndHour && x.Data.StartHour<end &&
                (x.ResourceIds.Intersect(ids).Any() || (x.Data.ProjectId==project && x.Data.PhaseId==phase)));
            if(conflict!=null)
            {
                string message="Double booking: "+source.Id+" overlaps "+conflict.Data.ProjectId+" / "+conflict.Data.PhaseId+" ["+conflict.Data.StartHour+", "+conflict.Data.EndHour+").";
                GameEvents.Publish(new ResourceBookingConflictEvent(project,phase,message));throw new InvalidOperationException(message);
            }
            var booking=new ScheduledAssignment(new ResourceBookingData("booking-"+(bookings.Count+1),project,phase,source.Id,start,end),source,equipment,ids);bookings.Add(booking);return booking;
        }
        public void NotifyBooking(ScheduledAssignment booking)=>GameEvents.Publish(new ScheduleChangedEvent(booking.Data.ProjectId));
        public void Block(ScheduledAssignment booking,string reason)
        {bool changed=booking.Data.Conflict!=reason;booking.Data.Status=BookingStatus.Blocked;booking.Data.Conflict=reason;if(changed){GameEvents.Publish(new ResourceBookingConflictEvent(booking.Data.ProjectId,booking.Data.PhaseId,reason));NotifyBooking(booking);}}
        public void Finish(ScheduledAssignment booking,BookingStatus status){booking.Data.Status=status;booking.Data.Conflict=null;}
        public void RecordProgress(string project,string phase,double before,double after,long endHour)
        {
            var row=Get(project).Phases.Single(x=>x.PhaseId==phase);
            if(after>before && !row.ActualStart.HasValue)row.ActualStart=endHour-1;
            if(after>=100 && !row.ActualCompletion.HasValue)row.ActualCompletion=endHour;
        }
        public void Update(long hour,IEnumerable<string> projectIds=null)
        {
            CurrentHour=hour;var ids=projectIds==null?null:new HashSet<string>(projectIds);
            foreach(var plan in plans.Values.Where(x=>(ids==null || ids.Contains(x.Project.Id)) && (x.Project.Status==ProjectStatus.Active || x.Project.Status==ProjectStatus.Complete)))
            {
                var schedule=plan.Project.Schedule;CriticalPathCalculator.Recalculate(schedule,plan.Phases,hour);
                foreach(var row in schedule.Phases)
                {
                    double expected=Math.Min(100,Math.Max(0,(hour-row.PlannedStart)/(row.PlannedCompletion-row.PlannedStart)*100));
                    bool late=!row.ActualCompletion.HasValue && row.Progress+1e-8<expected && row.IsCriticalPath;
                    if(late && plan.Reported.Add(row.PhaseId))GameEvents.Publish(new ProjectDelayedEvent(plan.Project.Id,(int)Math.Min(int.MaxValue,Math.Ceiling(row.CurrentDelay)),"Critical task behind plan: "+row.Name));
                    if(!late)plan.Reported.Remove(row.PhaseId);
                }
                GameEvents.Publish(new ScheduleChangedEvent(plan.Project.Id));
            }
        }
    }
}
