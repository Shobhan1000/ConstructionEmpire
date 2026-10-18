using System;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Market;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class MarketSystemTests
    {
        [SetUp]public void Setup()=>GameEvents.Clear();
        [TearDown]public void Cleanup()=>GameEvents.Clear();
        private static TickContext Tick(long hour)=>new TickContext(hour,24,7,28);
        private static ManagementSession Session(SimulationManager sim,MarketSettings settings)
        {var projects=SampleProjectDataFactory.CreateStarterProjects();return new ManagementSession(sim,projects,SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment(),projects.ToDictionary(x=>x.Id,x=>SampleRenovationPhaseFactory.CreateHouseRenovationPhases()),SampleProjectDataFactory.CreateRates(),SampleProjectDataFactory.CreateBiddingConfiguration(),SampleProjectDataFactory.CreateCompetitors(),ManagementSettings.CreateStarter(),marketSettings:settings);}
        [Test]public void DemandAndPricesRemainBoundedAcrossTenThousandCycles()
        {
            var settings=new MarketSettings {UpdateEveryHours=1,DemandStep=.5};var market=new MarketConditionsSystem(settings,new SeededRandom(42));
            for(int hour=1;hour<=10000;hour++){market.OnTick(Tick(hour));Assert.That(market.Current.Demand,Is.InRange(settings.MinimumDemand,settings.MaximumDemand));Assert.That(market.Current.MaterialIndex,Is.InRange(settings.MinimumPriceFactor,settings.MaximumPriceFactor));Assert.That(market.Current.WageIndex,Is.InRange(settings.MinimumPriceFactor,settings.MaximumPriceFactor));}
        }
        [Test]public void SameSeedAndTicksReplayIdenticalMarketIndices()
        {var config=new MarketSettings {UpdateEveryHours=3};var a=new MarketConditionsSystem(config,new SeededRandom(123));var b=new MarketConditionsSystem(config,new SeededRandom(123));for(int hour=1;hour<=400;hour++){a.OnTick(Tick(hour));b.OnTick(Tick(hour));Assert.That(a.Current.Demand,Is.EqualTo(b.Current.Demand));Assert.That(a.Current.MaterialIndex,Is.EqualTo(b.Current.MaterialIndex));Assert.That(a.Current.News,Is.EqualTo(b.Current.News));}}
        [Test]public void SeasonCalendarWrapsAndWinterHasLowerDemandHigherWeatherFrequency()
        {var s=new SeasonalitySystem(new MarketSettings(),24,28);long season=24*28*3;Assert.That(s.At(0),Is.EqualTo(Season.Spring));Assert.That(s.At(season),Is.EqualTo(Season.Summer));Assert.That(s.At(3*season),Is.EqualTo(Season.Winter));Assert.That(s.At(4*season),Is.EqualTo(Season.Spring));Assert.That(s.DemandMultiplier(3*season),Is.LessThan(s.DemandMultiplier(season)));Assert.That(s.WeatherFrequency(3*season),Is.GreaterThan(s.WeatherFrequency(season)));}
        [Test]public void MarketCadenceDoesNotPublishTwiceForSameTick()
        {var market=new MarketConditionsSystem(new MarketSettings {UpdateEveryHours=4},new SeededRandom(3));int calls=0;GameEvents.Subscribe<MarketChangedEvent>(_=>calls++);market.OnTick(Tick(3));Assert.That(calls,Is.Zero);market.OnTick(Tick(4));market.OnTick(Tick(4));Assert.That(calls,Is.EqualTo(1));}
        [Test]public void SharedMarketMovesSupplierBasesWagesEstimatesAndNewLoanQuotes()
        {
            using(var sim=new SimulationManager(new CompanyData("Market",250000),new GameTimeData(),42))using(var s=Session(sim,new MarketSettings {UpdateEveryHours=1,DemandStep=0,ShortageChance=0,OpportunityBaseCount=0,SeasonalDemand=new[]{1.5,1.5,1.5,1.5}}))
            {var offer=s.Suppliers.Suppliers[0].Offers[0];double price=offer.BasePrice,wage=s.Workforce.Workers[0].WagePerHour;var estimate=s.Estimation.Estimate(s.Projects[0],0,0);decimal rate=s.Finance.Banking.QuotedAnnualRate;sim.Advance(1);Assert.That(offer.BasePrice,Is.GreaterThan(price));Assert.That(s.Workforce.Workers[0].WagePerHour,Is.GreaterThan(wage));Assert.That(s.Estimation.Estimate(s.Projects[0],0,0).EstimatedCost,Is.GreaterThan(estimate.EstimatedCost));Assert.That(s.Finance.Banking.QuotedAnnualRate,Is.GreaterThan(rate));double once=s.Workforce.Workers[0].WagePerHour;sim.Advance(1);Assert.That(s.Workforce.Workers[0].WagePerHour,Is.EqualTo(once));}
        }
        [Test]public void CompetitorClaimsAreNamedVisibleOnceAndNeverStealActiveWork()
        {
            using(var sim=new SimulationManager(new CompanyData("AI",250000),new GameTimeData(),42))using(var s=Session(sim,new MarketSettings {CompetitorEveryHours=1,TenderGraceHours=1,CompetitorBidChance=1,CompetitorFailureChance=0,CompetitorDevelopmentChance=0}))
            {var protectedProject=s.Projects[0];protectedProject.Status=ProjectStatus.Active;int seen=0;GameEvents.Subscribe<CompetitorClaimedContractEvent>(e=>{Assert.That(e.CompetitorName,Is.Not.Empty);seen++;});sim.Advance(1);Assert.That(seen,Is.GreaterThan(0));Assert.That(protectedProject.Status,Is.EqualTo(ProjectStatus.Active));var lost=s.Projects.Count(x=>x.Status==ProjectStatus.Lost);Assert.That(s.Projects.Where(x=>x.Status==ProjectStatus.Lost).All(x=>!string.IsNullOrEmpty(x.AwardedCompetitorName)),Is.True);sim.Advance(1);Assert.That(seen,Is.EqualTo(s.Projects.Count(x=>x.Status==ProjectStatus.Lost)));Assert.That(seen,Is.GreaterThanOrEqualTo(lost));}
        }
        [Test]public void HigherDemandGeneratesMoreOpportunitiesOverControlledCycles()
        {var high=new MarketConditionsSystem(new MarketSettings {UpdateEveryHours=1,DemandStep=0,SeasonalDemand=new[]{1.7,1.7,1.7,1.7}},new SeededRandom(7));var low=new MarketConditionsSystem(new MarketSettings {UpdateEveryHours=1,DemandStep=0,SeasonalDemand=new[]{.4,.4,.4,.4}},new SeededRandom(7));int a=0,b=0;for(int i=1;i<=300;i++){high.OnTick(Tick(i));low.OnTick(Tick(i));a+=high.OpportunityCount();b+=low.OpportunityCount();}Assert.That(a,Is.GreaterThan(b*2));}
        [Test]public void InvalidBoundsAndSeasonProfilesAreRejected()
        {Assert.Throws<ArgumentException>(()=>new MarketConditionsSystem(new MarketSettings {MinimumDemand=2},new SeededRandom(1)));Assert.Throws<ArgumentException>(()=>new MarketConditionsSystem(new MarketSettings {SeasonalDemand=new[]{1d}},new SeededRandom(1)));}
    }
}
