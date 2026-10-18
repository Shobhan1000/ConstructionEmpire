using System;
using System.Collections.Generic;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Risk;
using ConstructionEmpire.Data.Events;
namespace ConstructionEmpire.Systems.Risk
{
    public sealed class SiteConditionsSystem
    {
        private readonly RiskSettings settings;private readonly Dictionary<string,long> stoppedUntil=new Dictionary<string,long>();
        public SiteConditionsSystem(RiskSettings settings){this.settings=settings;settings.Validate();}
        public long StoppedUntil(string id)=>stoppedUntil.TryGetValue(id,out var hour)?hour:0;
        public void Stop(string id,long now,int hours){stoppedUntil[id]=Math.Max(StoppedUntil(id),checked(now+hours));}
        public double Modifier(ProjectData project,long hour)
        {if(hour<=StoppedUntil(project.Id) && StoppedUntil(project.Id)>0)return 0;var p=project.Site;return Math.Max(.1,1-p.Difficulty*settings.SiteDifficultyPenalty-p.Soil*settings.SoilPenalty-p.AccessConstraint*settings.AccessPenalty-p.Remoteness*settings.RemotenessPenalty);}
        public void DiscoverPoorGround(ProjectData project,long hour)
        {project.Site.GroundConditionDiscovered=true;project.Site.Soil=Math.Max(project.Site.Soil,.8);Stop(project.Id,hour,settings.GroundStopHours);GameEvents.Publish(new SiteConditionDiscoveredEvent(project.Id,"Poor soil discovered. Ground investigation and remediation required; work is temporarily stopped."));GameEvents.Publish(new RiskStateChangedEvent(project.Id,"Ground conditions changed."));}
    }
}
