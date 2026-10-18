using System;
using System.Linq;
using System.Collections.Generic;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Expansion;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Randomness;
namespace ConstructionEmpire.Systems.Expansion
{
    public sealed class GeneratedOpportunity {public ProjectData Project;public List<ConstructionPhaseRuntimeData> Phases;}
    public static class OpportunityGenerator
    {
        public static GeneratedOpportunity Create(ProjectData template,IEnumerable<ConstructionPhaseRuntimeData> authored,OpportunityProfile profile,SeededRandom random,long hour,double regionalPrice)
        {
            profile.Validate();var source=authored.Select(x=>x.Copy()).ToList();new PhaseDependencyGraph(source);
            double originalLabor=source.Sum(x=>x.LaborHours);
            double originalMaterials=source.Sum(x=>x.MaterialRequirements.Sum(m=>m.Quantity));
            double originalEquipment=source.Sum(x=>x.DurationHours*x.EquipmentRequirements.Sum(e=>e.Count));
            double originalSubcontracting=source.Where(x=>x.RequiresSubcontractor).Sum(x=>x.LaborHours);
            var omitted=new HashSet<string>(profile.OmitPhases.Split('|'));var focus=new HashSet<string>(profile.FocusPhases.Split('|'));
            var tasks=source.Where(x=>!omitted.Contains(x.Id)).ToList();if(tasks.Count==0)throw new ArgumentException("Profile removes all work.");
            double scale=profile.MinimumScale+random.NextDouble()*(profile.MaximumScale-profile.MinimumScale);
            foreach(var phase in tasks)
            {
                var expanded=new HashSet<string>();foreach(var dependency in phase.PrerequisiteIds)Expand(dependency,source,omitted,expanded);phase.PrerequisiteIds=expanded.OrderBy(x=>x,StringComparer.Ordinal).ToList();
                double factor=scale*(focus.Contains(phase.Id)?profile.FocusMultiplier:profile.OtherMultiplier);
                phase.DurationHours*=factor;phase.LaborHours*=factor;phase.Cost*=factor;foreach(var m in phase.MaterialRequirements)m.Quantity*=factor;
                phase.Progress=0;phase.AccruedCost=0;phase.ReworkHoursRemaining=0;
            }
            new PhaseDependencyGraph(tasks);
            double workload=tasks.Sum(x=>x.LaborHours)/originalLabor;
            var p=template.Copy();p.Title=profile.Title;p.Size*=(float)scale;p.Budget*=(float)(workload*regionalPrice);p.EstimatedDurationHours*=(float)workload;
            p.DeadlineHour=checked(hour+(long)Math.Ceiling(template.DeadlineHour*workload*profile.DeadlineFactor));
            p.Requirements.LaborHours*=(float)workload;
            p.Requirements.MaterialUnits*=(float)(originalMaterials>0?tasks.Sum(x=>x.MaterialRequirements.Sum(m=>m.Quantity))/originalMaterials:workload);
            p.Requirements.EquipmentHours*=(float)(originalEquipment>0?tasks.Sum(x=>x.DurationHours*x.EquipmentRequirements.Sum(e=>e.Count))/originalEquipment:workload);
            p.Requirements.SubcontractorHours*=(float)(originalSubcontracting>0?tasks.Where(x=>x.RequiresSubcontractor).Sum(x=>x.LaborHours)/originalSubcontracting:workload);
            p.Site.Difficulty=profile.Difficulty;p.Site.Soil=random.NextDouble()*profile.Difficulty;p.Site.AccessConstraint=random.NextDouble()*profile.Difficulty;p.Site.DesignComplexity=Math.Min(1,profile.Difficulty+.1);
            p.Site.Description=profile.Title+"; "+(profile.DeadlineFactor<1?"tight delivery window":"planned delivery window")+"; "+(profile.FocusPhases.Replace('|',',')+" emphasis");
            int client=random.Next(BiddingFlavorContent.Clients.Length);p.ClientId="generated-client-"+client;p.ClientName=BiddingFlavorContent.Clients[client];
            p.Status=ProjectStatus.Available;p.Schedule=null;p.SubmittedBidId=null;p.SubmittedBidAmount=0;p.AwardedCompetitorId=null;p.AwardedCompetitorName=null;p.ContractValue=0;
            p.Validate();return new GeneratedOpportunity {Project=p,Phases=tasks};
        }
        private static void Expand(string id,List<ConstructionPhaseRuntimeData> phases,HashSet<string> omitted,HashSet<string> result)
        {if(!omitted.Contains(id)){result.Add(id);return;}foreach(var prerequisite in phases.Single(x=>x.Id==id).PrerequisiteIds)Expand(prerequisite,phases,omitted,result);}
    }
}
