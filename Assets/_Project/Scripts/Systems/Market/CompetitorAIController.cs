using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Market
{
    public sealed class CompetitorOperationData
    {public string Id;public int Bids,Wins,Losses,Scale=1,Employees,Equipment,Offices=1;internal int ReviewedWins,ReviewedLosses;}
    /// <summary>Lightweight aggregate operations. Exact rival offers stay inside the resolver.</summary>
    public sealed class CompetitorAIController : IRegisterableSystem,IDisposable
    {
        private readonly ManagementSession session;private readonly MarketSettings settings;private readonly Dictionary<string,long> firstSeen=new Dictionary<string,long>();private readonly Dictionary<string,CompetitorOperationData> operations=new Dictionary<string,CompetitorOperationData>();private readonly EventSubscriptions subscriptions=new EventSubscriptions();private long nextHour;
        public IReadOnlyList<CompetitorOperationData> Operations=>operations.Values.OrderBy(x=>x.Id,StringComparer.Ordinal).ToList();
        public CompetitorAIController(ManagementSession session,MarketSettings settings)
        {
            this.session=session;this.settings=settings;settings.Validate();nextHour=session.Simulation.Time.TotalHours+settings.CompetitorEveryHours;
            foreach(var c in session.Competitors)operations.Add(c.Id,new CompetitorOperationData {Id=c.Id,Employees=settings.InitialEmployees,Equipment=settings.InitialEquipment});foreach(var p in session.Projects)firstSeen[p.Id]=session.Simulation.Time.TotalHours;
            subscriptions.Subscribe<CompetitorClaimedContractEvent>(e=>{if(session.Find(e.ProjectId)==null || !operations.ContainsKey(e.CompetitorId))return;Record(e.CompetitorId,true);});
            subscriptions.Subscribe<BidWonEvent>(e=>{var p=session.Find(e.ProjectId);if(p!=null)foreach(var c in session.Expansion.CompetitorsFor(p))Record(c.Id,false);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Expansion.OpportunitiesChangedEvent>(_=>{foreach(var p in session.Projects)if(!firstSeen.ContainsKey(p.Id))firstSeen[p.Id]=session.Simulation.Time.TotalHours;});
        }
        private void Record(string id,bool won)
        {var o=operations[id];o.Bids++;if(won)o.Wins++;else o.Losses++;var profile=session.Competitors.Single(x=>x.Id==id);profile.Reputation=(float)Math.Max(0,Math.Min(100,profile.Reputation+(won?settings.ReputationPerWin:-settings.ReputationPerLoss)));}
        public void OnTick(TickContext tick)
        {
            if(tick.TotalHours<nextHour)return;nextHour=checked(tick.TotalHours+settings.CompetitorEveryHours);var rng=session.Simulation.Random;var awardedThisCycle=new Dictionary<string,int>();
            foreach(var p in session.Projects.Where(x=>x.Status==ProjectStatus.Available).OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray())
            {
                if(!firstSeen.TryGetValue(p.Id,out long seen)){firstSeen[p.Id]=tick.TotalHours;continue;}if(tick.TotalHours-seen<settings.TenderGraceHours)continue;
                var bids=new List<(CompetitorCompanyData Profile,double Score)>();
                foreach(var c in session.Expansion.CompetitorsFor(p).OrderBy(x=>x.Id,StringComparer.Ordinal))
                {
                    if(awardedThisCycle.TryGetValue(c.Id,out int awarded) && awarded>=operations[c.Id].Scale)continue;
                    double interest=settings.CompetitorBidChance/Math.Max(.5,session.Market.Current.Demand);if(rng.NextDouble()>=Math.Min(1,interest))continue;
                    double price=c.PriceMultiplier*(1+(rng.NextDouble()*2-1)*settings.PriceVariation);double score=1/price+c.Quality+c.Reputation/100+1/c.DurationMultiplier+(c.Specialty==p.BuildingType?c.CategoryStrength:0)+(c.HomeRegion==p.Region?c.RegionalStrength:0);bids.Add((c,score*(.9+.2*rng.NextDouble())));
                }
                if(bids.Count==0)continue;var winner=bids.OrderByDescending(x=>x.Score).ThenBy(x=>x.Profile.Id,StringComparer.Ordinal).First().Profile;
                awardedThisCycle[winner.Id]=awardedThisCycle.TryGetValue(winner.Id,out int previous)?previous+1:1;
                foreach(var bid in bids.Where(x=>x.Profile.Id!=winner.Id))Record(bid.Profile.Id,false);
                p.Status=ProjectStatus.Lost;p.AwardedCompetitorId=winner.Id;p.AwardedCompetitorName=winner.CompanyName;
                GameEvents.Publish(new CompetitorClaimedContractEvent(p.Id,winner.Id,winner.CompanyName,false));
            }
            foreach(var c in session.Competitors.OrderBy(x=>x.Id,StringComparer.Ordinal))
            {
                var o=operations[c.Id];if(rng.NextDouble()<settings.CompetitorFailureChance){Record(c.Id,false);GameEvents.Publish(new CompetitorDevelopmentEvent(c.Id,c.CompanyName+" reports a delivery setback; reputation has weakened."));}
                if(rng.NextDouble()>=settings.CompetitorDevelopmentChance)continue;
                if(o.Wins-o.ReviewedWins>=settings.WinsForGrowth && o.Scale<settings.MaximumScale){o.Scale++;o.Employees+=settings.InitialEmployees;o.Equipment+=settings.InitialEquipment;o.Offices++;o.ReviewedWins=o.Wins;GameEvents.Publish(new CompetitorDevelopmentEvent(c.Id,c.CompanyName+" expands: hires staff, buys equipment and opens another office."));}
                else if(o.Losses-o.ReviewedLosses>=settings.LossesForShrink && o.Scale>1){o.Scale--;o.Employees-=settings.InitialEmployees;o.Equipment-=settings.InitialEquipment;o.Offices--;o.ReviewedLosses=o.Losses;GameEvents.Publish(new CompetitorDevelopmentEvent(c.Id,c.CompanyName+" reduces its workforce and fleet after a weak run of tenders."));}
            }
        }
        public void Dispose()=>subscriptions.Dispose();
    }
}
