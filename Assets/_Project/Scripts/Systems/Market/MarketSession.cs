using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Systems.Management;
namespace ConstructionEmpire.Systems.Market
{
    /// <summary>Applies aggregate market indices to runtime prices, never definition assets or existing loans/orders.</summary>
    public sealed class MarketSession : IDisposable
    {
        private readonly ManagementSession session;private readonly EventSubscriptions subscriptions=new EventSubscriptions();private readonly Dictionary<EmployeeData,double> wageBases=new Dictionary<EmployeeData,double>();
        public MarketConditionsSystem Conditions {get;}public CompetitorAIController Competitors {get;}
        public MarketSession(ManagementSession session,MarketSettings settings)
        {
            this.session=session;var clock=session.Simulation.Time.CaptureState();Conditions=new MarketConditionsSystem(settings,session.Simulation.Random,clock.HoursPerDay,clock.DaysPerMonth,clock.TotalHours);Competitors=new CompetitorAIController(session,settings);
            session.Bidding.MarketPricePressure=settings.PricePressure;session.Risks.Weather.SeasonalFrequency=Conditions.Seasons.WeatherFrequency;
            subscriptions.Subscribe<MarketChangedEvent>(e=>{if(ReferenceEquals(e.Snapshot,Conditions.Current))Apply(e.Snapshot);});
            session.Simulation.RegisterBefore(Conditions,session.Risks);session.Simulation.Register(Competitors);
        }
        private void Apply(MarketSnapshot market)
        {
            session.Suppliers.SetMarketIndex(market.MaterialIndex);session.Estimation.MaterialMarketIndex=market.MaterialIndex;session.Estimation.LaborMarketIndex=market.WageIndex;session.Estimation.EquipmentMarketIndex=market.EquipmentIndex;
            foreach(var worker in session.Workforce.Workers.Concat(session.Expansion.Offices.SelectMany(x=>x.RegionalWorkforcePool)).Distinct())
            {if(!wageBases.ContainsKey(worker))wageBases.Add(worker,worker.WagePerHour);worker.WagePerHour=wageBases[worker]*market.WageIndex;}
            session.Finance.Banking.MarketInterestAdjustment=(decimal)market.InterestAdjustment;session.Bidding.MarketDemand=market.Demand;
            foreach(var office in session.Expansion.Offices.OrderBy(x=>x.Id,StringComparer.Ordinal))
            {int available=session.Expansion.Opportunities(office.RegionId).Count;int capacity=session.Expansion.Settings.MaximumAvailableOpportunitiesPerRegion-available;int count=Math.Min(capacity,Conditions.OpportunityCount());if(count>0)session.Expansion.GenerateOpportunities(office.Id,count);}
            foreach(var p in session.Projects)session.Risks.Forecasts.Refresh(p.Id);GameEvents.Publish(new ResourcesChangedEvent(null));
        }
        public void Dispose(){subscriptions.Dispose();Competitors.Dispose();session.Simulation.Unregister(Conditions);session.Simulation.Unregister(Competitors);session.Risks.Weather.SeasonalFrequency=null;}
    }
}
