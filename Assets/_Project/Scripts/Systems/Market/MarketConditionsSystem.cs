using System;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Market
{
    public sealed class MarketConditionsSystem : IRegisterableSystem
    {
        private readonly MarketSettings settings;private readonly SeededRandom random;private long nextHour;private double underlyingDemand=1;
        public SeasonalitySystem Seasons {get;}public MarketSnapshot Current {get;private set;}
        public MarketConditionsSystem(MarketSettings settings,SeededRandom random,int hoursPerDay=24,int daysPerMonth=28,long startHour=0)
        {this.settings=settings??throw new ArgumentNullException(nameof(settings));settings.Validate();this.random=random??throw new ArgumentNullException(nameof(random));Seasons=new SeasonalitySystem(settings,hoursPerDay,daysPerMonth);nextHour=checked(startHour+settings.UpdateEveryHours);Current=new MarketSnapshot(startHour,MarketState.Normal,Seasons.At(startHour),1,1,1,1,1,0,"Construction market steady: demand and available capacity remain broadly balanced.");}
        private double Price(double x)=>Math.Max(settings.MinimumPriceFactor,Math.Min(settings.MaximumPriceFactor,x));
        private MarketSnapshot Snapshot(long hour,bool materialShortage,bool laborShortage)
        {
            double demand=Math.Max(settings.MinimumDemand,Math.Min(settings.MaximumDemand,underlyingDemand*Seasons.DemandMultiplier(hour)));
            var state=demand<.6?MarketState.Recession:demand<.85?MarketState.Slow:demand<1.15?MarketState.Normal:demand<1.5?MarketState.Growth:MarketState.Boom;
            double material=Price(1+(demand-1)*settings.MaterialSensitivity+(materialShortage?settings.MaterialShortagePremium:0));double wage=Price(1+(demand-1)*settings.WageSensitivity+(laborShortage?settings.LaborShortagePremium:0));
            string news=materialShortage?MarketNewsContent.Materials:laborShortage?MarketNewsContent.Labor:Seasons.At(hour)==Season.Winter?MarketNewsContent.Winter:state==MarketState.Boom?MarketNewsContent.Boom:state==MarketState.Recession || state==MarketState.Slow?MarketNewsContent.Recession:state==MarketState.Growth?MarketNewsContent.Infrastructure:"Construction market steady: demand and available capacity remain broadly balanced.";
            return new MarketSnapshot(hour,state,Seasons.At(hour),demand,material,wage,Price(1+(demand-1)*settings.EquipmentSensitivity),Math.Max(.2,Math.Min(1,1-(wage-1))),Math.Max(-.05,Math.Min(.1,(demand-1)*settings.InterestSensitivity)),news);
        }
        public int OpportunityCount(double regionalMultiplier=1)
        {if(double.IsNaN(regionalMultiplier)||double.IsInfinity(regionalMultiplier)||regionalMultiplier<0)throw new ArgumentOutOfRangeException(nameof(regionalMultiplier));double expected=settings.OpportunityBaseCount*Current.Demand*regionalMultiplier;int whole=(int)Math.Floor(expected);return whole+(random.NextDouble()<expected-whole?1:0);}
        public void OnTick(TickContext tick)
        {
            while(tick.TotalHours>=nextHour)
            {
                underlyingDemand=Math.Max(settings.MinimumDemand,Math.Min(settings.MaximumDemand,underlyingDemand+(random.NextDouble()*2-1)*settings.DemandStep+(1-underlyingDemand)*settings.MeanReversion));
                Current=Snapshot(nextHour,random.NextDouble()<settings.ShortageChance,random.NextDouble()<settings.ShortageChance);nextHour=checked(nextHour+settings.UpdateEveryHours);GameEvents.Publish(new MarketChangedEvent(Current));
            }
        }
    }
}
