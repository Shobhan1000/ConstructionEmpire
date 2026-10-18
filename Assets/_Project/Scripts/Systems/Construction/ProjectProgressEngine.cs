using ConstructionEmpire.Systems.Scheduling;
using ConstructionEmpire.Data.Scheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Construction
{
    /// <summary>Register once with SimulationManager. Assign resources explicitly to eligible phases.</summary>
    public sealed class ProjectProgressEngine : IRegisterableSystem
    {
        // Numerical precision only, not a gameplay threshold (percentage points).
        private const double CompletionEpsilon = 1e-9;
        private sealed class Assignment
        {
            public string PhaseId;
            public IPhaseWorkSource Source;
            public List<EquipmentRuntimeData> Equipment;
            public ScheduledAssignment Booking;
        }
        private sealed class Job
        {
            public ProjectData Project;
            public List<ConstructionPhaseRuntimeData> Phases;
            public MaterialInventoryData Inventory;
            public List<Assignment> Assignments = new List<Assignment>();
            public double Site=1, Weather=1;
        }
        private readonly Dictionary<string,Job> jobs=new Dictionary<string,Job>(StringComparer.Ordinal);
        private readonly CrewEfficiencyConfiguration configuration;
        private bool ticking;
        public IPhaseConditions Conditions {get;set;}
        public bool HasProjects=>jobs.Count>0;
        public bool ContainsProject(string id)=>jobs.ContainsKey(id);
        public SchedulingSystem Scheduling {get;private set;}
        public void EnableScheduling(SchedulingSystem system)
        {if(ReferenceEquals(Scheduling,system))return;if(jobs.Count>0)throw new InvalidOperationException("Enable scheduling before starting projects.");Scheduling=system??throw new ArgumentNullException(nameof(system));}
        public ScheduledAssignment ScheduleCrew(string project,string phase,CrewData crew,IEnumerable<EquipmentRuntimeData> equipment,long start,long end)
        {
            if(Scheduling==null)throw new InvalidOperationException("Scheduling is not enabled.");
            var booking=Scheduling.Reserve(project,phase,new InternalCrewWorkSource(crew),equipment,start,end);Scheduling.NotifyBooking(booking);return booking;
        }
        public ProjectProgressEngine(CrewEfficiencyConfiguration configuration)
        { if(configuration==null)throw new ArgumentNullException(nameof(configuration)); configuration.Validate(); this.configuration=new CrewEfficiencyConfiguration {UnmatchedTradeMultiplier=configuration.UnmatchedTradeMultiplier,SkillFloor=configuration.SkillFloor,ExperienceBonusPerYear=configuration.ExperienceBonusPerYear,MaximumExperienceBonus=configuration.MaximumExperienceBonus,NoSupervisorMultiplier=configuration.NoSupervisorMultiplier,SupervisorSkillBonus=configuration.SupervisorSkillBonus,MaximumFatiguePenalty=configuration.MaximumFatiguePenalty}; }

        public void StartProject(ProjectData project,IEnumerable<ConstructionPhaseRuntimeData> phases,MaterialInventoryData inventory,long startHour=0)
        {
            if(ticking) throw new InvalidOperationException("Project edits take place between ticks.");
            if(project==null || phases==null || inventory==null) throw new ArgumentNullException();
            project.Validate(); inventory.Validate(); var list=phases.ToList(); new PhaseDependencyGraph(list);
            if(project.Status!=ProjectStatus.Won || jobs.ContainsKey(project.Id) || list.Any(p=>p.Progress!=0)) throw new InvalidOperationException("Start requires a won project with fresh phases and a unique ID.");
            Scheduling?.Track(project,list,startHour);
            jobs.Add(project.Id,new Job {Project=project,Phases=list,Inventory=inventory}); project.Status=ProjectStatus.Active;
            GameEvents.Publish(new ProjectStartedEvent(project.Id));
        }
        public void SetEnvironment(string projectId,double siteConditions,double weatherModifier)
        {
            ConstructionValidation.NonNegative(siteConditions,nameof(siteConditions)); ConstructionValidation.NonNegative(weatherModifier,nameof(weatherModifier));
            var job=Get(projectId); job.Site=siteConditions; job.Weather=weatherModifier;
        }
        public IReadOnlyList<ConstructionPhaseRuntimeData> GetPhases(string projectId) => Get(projectId).Phases.AsReadOnly();
        public void AssignCrew(string projectId,string phaseId,CrewData crew,IEnumerable<EquipmentRuntimeData> equipment)
        { AssignWorkSource(projectId,phaseId,new InternalCrewWorkSource(crew),equipment); }
        public void AssignWorkSource(string projectId,string phaseId,IPhaseWorkSource source,IEnumerable<EquipmentRuntimeData> equipment)
        { AssignCore(projectId,phaseId,source,equipment,null); GameEvents.Publish(new ResourcesChangedEvent(projectId)); }
        private void AssignCore(string projectId,string phaseId,IPhaseWorkSource source,IEnumerable<EquipmentRuntimeData> equipment,ScheduledAssignment booking)
        {
            if(ticking) throw new InvalidOperationException("Assignments change between ticks.");
            var job=Get(projectId); if(source==null || equipment==null) throw new ArgumentNullException();
            source.Validate();if(!source.CanPerform(job.Phases.Single(x=>x.Id==phaseId)))throw new InvalidOperationException("This phase requires a matching work provider."); var machines=equipment.ToList(); foreach(var machine in machines) { if(machine==null) throw new ArgumentException("Null equipment."); machine.Validate(); }
            if(job.Project.Status!=ProjectStatus.Active || !new PhaseDependencyGraph(job.Phases).CanStart(phaseId)) throw new InvalidOperationException("Prerequisites must be complete before assignment.");
            if(job.Assignments.Any(a=>a.PhaseId==phaseId)) throw new InvalidOperationException("Phase already has a crew.");
            if(machines.Any(e=>e.Status!=EquipmentStatus.Available)) throw new InvalidOperationException("Equipment is not available.");
            if(machines.Select(x=>x.Id).Distinct().Count()!=machines.Count) throw new ArgumentException("Duplicate equipment.");
            var all=jobs.Values.SelectMany(j=>j.Assignments).ToList();
            if(all.Any(a=>a.Source.Id==source.Id || a.Source.WorkerIds.Intersect(source.WorkerIds).Any() || a.Equipment.Any(e=>machines.Any(n=>n.Id==e.Id))) ||
                source.IsClaimed || machines.Any(e=>!string.IsNullOrEmpty(e.CurrentProjectId)))
                throw new InvalidOperationException("Crew, worker or equipment is already assigned.");
            if(Scheduling!=null && booking==null)booking=Scheduling.Reserve(projectId,phaseId,source,machines,Scheduling.CurrentHour,long.MaxValue);
            try {source.Claim(projectId,phaseId);}catch {if(booking!=null)Scheduling.Finish(booking,BookingStatus.Released);throw;}
            if(booking!=null)booking.Data.Status=BookingStatus.Active;
            foreach(var machine in machines) { machine.CurrentProjectId=projectId; machine.CurrentTaskId=phaseId; machine.Location=job.Project.Location; machine.Status=EquipmentStatus.Working; }
            job.Assignments.Add(new Assignment {PhaseId=phaseId,Source=source,Equipment=machines,Booking=booking});
            if(booking!=null)Scheduling.NotifyBooking(booking);
        }
        public void CancelBooking(string id)
        {
            if(ticking)throw new InvalidOperationException("Edit reservations between ticks.");
            var booking=Scheduling.Bookings.Single(x=>x.Data.Id==id);if(!(booking.Source is InternalCrewWorkSource))throw new InvalidOperationException("Awarded subcontractor commitments cannot be cancelled as crew reservations.");var job=Get(booking.Data.ProjectId);var assignment=job.Assignments.FirstOrDefault(x=>x.Booking==booking);
            if(assignment!=null)Release(job,assignment);else Scheduling.Finish(booking,BookingStatus.Released);Scheduling.NotifyBooking(booking);
        }
        public void ReleaseCrew(string projectId,string phaseId)
        {
            if(ticking) throw new InvalidOperationException("Assignments change between ticks.");
            var job=Get(projectId); var assignment=job.Assignments.Find(a=>a.PhaseId==phaseId);
            if(assignment!=null) { Release(job,assignment); GameEvents.Publish(new ResourcesChangedEvent(projectId)); }
        }
        private void Release(Job job,Assignment assignment)
        {
            assignment.Source.Release();
            if(assignment.Booking!=null)Scheduling.Finish(assignment.Booking,job.Phases.Single(x=>x.Id==assignment.PhaseId).IsComplete ? BookingStatus.Complete : BookingStatus.Released);
            foreach(var machine in assignment.Equipment) { machine.CurrentProjectId=null; machine.CurrentTaskId=null; if(machine.Status==EquipmentStatus.Working || machine.Status==EquipmentStatus.Assigned) machine.Status=EquipmentStatus.Available; }
            job.Assignments.Remove(assignment);
        }
        private Job Get(string id) { if(!jobs.TryGetValue(id,out var job)) throw new ArgumentException("Unknown execution project: "+id); return job; }

        public void ReleaseExpiredBookings(long hour)
        {
            if(Scheduling==null)return;var changed=new HashSet<string>();
                foreach(var job in jobs.Values)
                    foreach(var assignment in job.Assignments.Where(x=>x.Booking!=null && x.Booking.Data.EndHour<hour).ToArray())
                    {var booking=assignment.Booking;Release(job,assignment);Scheduling.Finish(booking,BookingStatus.Expired);changed.Add(job.Project.Id);}
            foreach(var id in changed)GameEvents.Publish(new ResourcesChangedEvent(id));
        }

        public void OnTick(TickContext context)
        {
            if(ticking) throw new InvalidOperationException("Reentrant construction tick.");
            if(Scheduling!=null)
            {
                ReleaseExpiredBookings(context.TotalHours);
                foreach(var booking in Scheduling.Bookings.Where(x=>jobs.ContainsKey(x.Data.ProjectId) && x.Open && x.Data.Status!=BookingStatus.Active).ToArray())
                {
                    if(Get(booking.Data.ProjectId).Phases.Single(x=>x.Id==booking.Data.PhaseId).IsComplete){Scheduling.Finish(booking,BookingStatus.Complete);continue;}
                    if(booking.Data.EndHour<context.TotalHours){Scheduling.Finish(booking,BookingStatus.Expired);continue;}
                    if(booking.Data.StartHour>=context.TotalHours)continue;
                    try {AssignCore(booking.Data.ProjectId,booking.Data.PhaseId,booking.Source,booking.Equipment,booking);}
                    catch(InvalidOperationException e){Scheduling.Block(booking,e.Message);}
                }
            }
            ticking=true;
            // Commit the entire tick before dispatching events so listeners cannot cause partial completion.
            var notifications=new List<PhaseProgressChangedEvent>(); var completed=new List<string>(); var work=new List<ConstructionWorkAccruedEvent>();
            try
            {
                foreach(var job in jobs.Values.OrderBy(j=>j.Project.Id,StringComparer.Ordinal))
                {
                    if(job.Project.Status!=ProjectStatus.Active) continue;
                    var graph=new PhaseDependencyGraph(job.Phases); job.Inventory.Validate();
                    foreach(var assignment in job.Assignments.OrderBy(a=>a.PhaseId,StringComparer.Ordinal).ToArray())
                    {
                        var phase=job.Phases.Find(p=>p.Id==assignment.PhaseId);
                        if(!graph.CanStart(phase.Id)) continue;
                        double before=phase.Progress;
                        if(assignment.Source.IsAvailable(context.TotalHours)) Advance(job,phase,assignment,context.ElapsedGameHours,context.TotalHours,work);
                        Scheduling?.RecordProgress(job.Project.Id,phase.Id,before,phase.Progress,context.TotalHours);
                        notifications.Add(new PhaseProgressChangedEvent(job.Project.Id,phase.Id,phase.Progress));
                        if(phase.IsComplete) Release(job,assignment);
                    }
                    if(job.Phases.All(p=>p.IsComplete)) { job.Project.Status=ProjectStatus.Complete; completed.Add(job.Project.Id); }
                }
            }
            finally { ticking=false; }
            var failures=new List<Exception>();
            try {Scheduling?.Update(context.TotalHours,jobs.Keys);}catch(Exception e){failures.Add(e);}
            foreach(var item in work) { try { GameEvents.Publish(item); } catch(Exception e) { failures.Add(e); } }
            foreach(var notification in notifications) { try { GameEvents.Publish(notification); } catch(Exception e) { failures.Add(e); } }
            foreach(var id in completed) { try { GameEvents.Publish(new ProjectCompletedEvent(id)); } catch(Exception e) { failures.Add(e); } }
            if(failures.Count>0) throw new AggregateException("Construction event listener failed after committed tick.",failures);
        }
        private void Advance(Job job,ConstructionPhaseRuntimeData phase,Assignment assignment,double hours,long hour,List<ConstructionWorkAccruedEvent> work)
        {
            var source=assignment.Source; source.Validate();
            foreach(var machine in assignment.Equipment) machine.Validate();
            // Each requirement is a simultaneous machine count. Only required machines burn fuel.
            var used=new List<EquipmentRuntimeData>(); double equipment=phase.EquipmentRequirements.Count==0 ? 1 : double.PositiveInfinity, fuelHours=hours;
            foreach(var requirement in phase.EquipmentRequirements)
            {
                var available=assignment.Equipment.Where(e=>e.EquipmentType==requirement.Type && (e.Status==EquipmentStatus.Working || e.Status==EquipmentStatus.Assigned) && e.Condition>0 && (e.EffectiveFuelPerHour==0 || e.CurrentFuel>0))
                    .OrderBy(e=>e.Id,StringComparer.Ordinal).Take(requirement.Count).ToList();
                if(available.Count<requirement.Count) return;
                equipment=Math.Min(equipment,available.Average(e=>e.Condition*e.ProductivityMultiplier));
                used.AddRange(available);
            }
            foreach(var machine in used) if(machine.EffectiveFuelPerHour>0) fuelHours=Math.Min(fuelHours,machine.CurrentFuel/machine.EffectiveFuelPerHour);
            double laborEfficiency=source.LaborEfficiency(phase,configuration);
            // Duration is the baseline at required labor staffing. Labor shortage lowers the base rate.
            double baseRate=source.BaseProgressPerHour(phase);
            double site=Conditions?.SiteModifier(job.Project,phase,hour)??job.Site;
            double weather=Conditions?.WeatherModifier(job.Project,phase)??job.Weather;
            var unconstrained=new ProgressFactors(baseRate,laborEfficiency,equipment,1,site,weather);
            if(unconstrained.Progress>0 && phase.ReworkHoursRemaining>0)
            {
                double rework=Math.Min(fuelHours,phase.ReworkHoursRemaining);phase.ReworkHoursRemaining-=rework;
                foreach(var machine in used)machine.CurrentFuel=Math.Max(0,machine.CurrentFuel-machine.EffectiveFuelPerHour*rework);
                double cost=(source.WagePerHour+used.Sum(e=>e.CostPerHour))*rework;phase.AccruedCost+=cost;
                work.Add(new ConstructionWorkAccruedEvent(job.Project.Id,source.WagePerHour*rework,used.Sum(e=>e.CostPerHour)*rework,0,rework,used.ToArray()));return;
            }
            double potential=Math.Min(100-phase.Progress,unconstrained.Progress*fuelHours);
            if(potential<=0) return;
            double materialAvailability=1;
            foreach(var material in phase.MaterialRequirements)
                materialAvailability=Math.Min(materialAvailability,job.Inventory.GetQuantity(material.MaterialId)/(material.Quantity*potential/100));
            var factors=new ProgressFactors(baseRate,laborEfficiency,equipment,materialAvailability,site,weather);
            double delta=Math.Min(100-phase.Progress,factors.Progress*fuelHours);
            // Availability is relative to capped potential, so cap again to the stocked fraction.
            delta=Math.Min(delta,potential*materialAvailability);
            if(delta<=0) { phase.AccruedCost+=source.WagePerHour*hours;work.Add(new ConstructionWorkAccruedEvent(job.Project.Id,source.WagePerHour*hours,0,0,0)); return; }
            double usedHours=delta/unconstrained.Progress;
            foreach(var material in phase.MaterialRequirements)
            {
                double consume=Math.Min(job.Inventory.GetQuantity(material.MaterialId),material.Quantity*delta/100);
                if(!job.Inventory.TryConsume(material.MaterialId,consume)) throw new InvalidOperationException("Material stock changed during tick.");
            }
            foreach(var machine in used) machine.CurrentFuel=Math.Max(0,machine.CurrentFuel-machine.EffectiveFuelPerHour*usedHours);
            phase.AccruedCost+=phase.Cost*delta/100 + (source.WagePerHour+used.Sum(e=>e.CostPerHour))*usedHours;
            work.Add(new ConstructionWorkAccruedEvent(job.Project.Id,source.WagePerHour*usedHours,used.Sum(e=>e.CostPerHour)*usedHours,phase.Cost*delta/100,usedHours,used.ToArray()));
            double previous=phase.Progress;
            phase.Progress=Math.Min(100,phase.Progress+delta);
            if(100-phase.Progress <= CompletionEpsilon)
            { phase.Progress=previous; bool accepted=Conditions==null || Conditions.CanComplete(job.Project,phase,hour);phase.Progress=accepted?100:previous; }
        }
    }
}
