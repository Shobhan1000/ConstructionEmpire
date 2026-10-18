using System;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Research;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Systems.Research;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class ResearchSystemTests
    {
        [SetUp]public void Setup()=>GameEvents.Clear();[TearDown]public void Cleanup()=>GameEvents.Clear();
        private static TickContext Tick(long hour)=>new TickContext(hour,24,7,28);
        private static ResearchSystem Research(Action<decimal,string> pay=null)=>new ResearchSystem(DefaultResearchTreeFactory.CreateStarterTree(),()=>100000,pay??((_,__)=>{}));
        private static ManagementSession Session(SimulationManager sim,ResearchSettings settings=null)
        {var projects=SampleProjectDataFactory.CreateStarterProjects();return new ManagementSession(sim,projects,SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment(),projects.ToDictionary(x=>x.Id,x=>SampleRenovationPhaseFactory.CreateHouseRenovationPhases()),SampleProjectDataFactory.CreateRates(),SampleProjectDataFactory.CreateBiddingConfiguration(),SampleProjectDataFactory.CreateCompetitors(),ManagementSettings.CreateStarter(),marketSettings:new MarketSettings {OpportunityBaseCount=0,CompetitorBidChance=0},researchSettings:settings);}
        [Test]public void ModifierOnlyAppliesAfterDurationAndCostIsChargedExactlyOnce()
        {decimal paid=0;var r=Research((cost,_)=>paid+=cost);Assert.That(r.Modifier(ResearchEffectKind.EstimationAccuracy),Is.Zero);r.Start("takeoffs",0);Assert.That(paid,Is.EqualTo(1500));r.OnTick(Tick(7));Assert.That(r.Modifier(ResearchEffectKind.EstimationAccuracy),Is.Zero);r.OnTick(Tick(8));Assert.That(r.Modifier(ResearchEffectKind.EstimationAccuracy),Is.EqualTo(.15));r.OnTick(Tick(8));Assert.That(paid,Is.EqualTo(1500));Assert.Throws<InvalidOperationException>(()=>r.Start("takeoffs",8));}
        [Test]public void MoneyPrerequisitesAndCompanyLevelGateResearch()
        {var list=DefaultResearchTreeFactory.CreateStarterTree();list[0].MinimumCompanyLevel=2;decimal cash=0;var r=new ResearchSystem(list,()=>cash,(_,__)=>{});Assert.Throws<InvalidOperationException>(()=>r.Start("takeoffs",0));r.SetCompanyLevel(2);Assert.Throws<InvalidOperationException>(()=>r.Start("takeoffs",0));cash=10000;r.Start("takeoffs",0);Assert.Throws<InvalidOperationException>(()=>r.Start("fuel",0));r.OnTick(Tick(8));Assert.Throws<InvalidOperationException>(()=>r.Start("fuel",8));}
        [Test]public void AvailabilityPublishesOncePerTransition()
        {var r=Research();int events=0;GameEvents.Subscribe<ResearchAvailableEvent>(e=>{if(ReferenceEquals(e.Owner,r))events++;});r.RefreshAvailability();r.RefreshAvailability();Assert.That(events,Is.EqualTo(1));r.Start("takeoffs",0);r.OnTick(Tick(8));Assert.That(events,Is.EqualTo(3));r.RefreshAvailability();Assert.That(events,Is.EqualTo(3));}
        [Test]public void InvalidGraphsAndDefinitionsAreRejected()
        {var list=DefaultResearchTreeFactory.CreateStarterTree();list[0].PrerequisiteIds.Add("fuel");Assert.Throws<ArgumentException>(()=>new ResearchSystem(list,()=>100000,(_,__)=>{}));list[0].PrerequisiteIds[0]="missing";Assert.Throws<ArgumentException>(()=>new ResearchSystem(list,()=>100000,(_,__)=>{}));list[0].Cost=double.NaN;Assert.Throws<ArgumentOutOfRangeException>(()=>new ResearchSystem(list,()=>100000,(_,__)=>{}));}
        [Test]public void TreeCopiesDoNotPermitMutationOfRuntimeDefinitions()
        {var list=DefaultResearchTreeFactory.CreateStarterTree();var r=new ResearchSystem(list,()=>100000,(_,__)=>{});list[0].Cost=0;r.Upgrades[0].Effects[0].Amount=1;r.Start("takeoffs",0);r.OnTick(Tick(8));Assert.That(r.Modifier(ResearchEffectKind.EstimationAccuracy),Is.EqualTo(.15));}
        [Test]public void FailedPaymentLeavesNoActiveResearch()
        {var r=Research((_,__)=>throw new InvalidOperationException("Payment rejected"));Assert.Throws<InvalidOperationException>(()=>r.Start("takeoffs",0));Assert.That(r.ActiveId,Is.Null);Assert.That(r.IsUnlocked("takeoffs"),Is.False);}
        [Test]public void AccuracyNarrowsEstimateRangeWithoutChangingBidPrice()
        {using(var sim=new SimulationManager(new CompanyData("Research",100000),new GameTimeData(),42))using(var s=Session(sim)){var before=s.Estimation.Estimate(s.Projects[0],.1,.1);s.Research.System.Start("takeoffs",0);for(int i=0;i<8;i++)sim.Advance(1);var after=s.Estimation.Estimate(s.Projects[0],.1,.1);Assert.That(after.Accuracy,Is.EqualTo(.85).Within(1e-10));Assert.That(after.UpperExpectedCost-after.LowerExpectedCost,Is.LessThan(before.UpperExpectedCost-before.LowerExpectedCost));Assert.That(after.BidPrice,Is.EqualTo(before.BidPrice));}}
        [Test]public void CrewModifierReachesInternalWorkSourceAndDoesNotCompound()
        {using(var sim=new SimulationManager(new CompanyData("Crew",100000),new GameTimeData(),42))using(var s=Session(sim)){var phase=s.Phases(s.Projects[0].Id)[0];var source=new InternalCrewWorkSource(s.Crew);var config=new CrewEfficiencyConfiguration();double before=source.LaborEfficiency(phase,config);s.Research.System.Start("takeoffs",0);s.Research.System.OnTick(Tick(8));s.Research.System.Start("crew-planning",8);s.Research.System.OnTick(Tick(20));s.Research.Apply();s.Research.Apply();Assert.That(source.LaborEfficiency(phase,config),Is.EqualTo(before*1.08).Within(1e-10));}}
        [Test]public void ConcurrentSessionsDoNotShareResearchEffects()
        {using(var a=new SimulationManager(new CompanyData("A",100000),new GameTimeData(),42))using(var b=new SimulationManager(new CompanyData("B",100000),new GameTimeData(),42))using(var sa=Session(a))using(var sb=Session(b)){sa.Research.System.Start("takeoffs",0);sa.Research.System.OnTick(Tick(8));Assert.That(sa.Estimation.EffectiveAccuracy,Is.EqualTo(.85).Within(1e-10));Assert.That(sb.Estimation.EffectiveAccuracy,Is.EqualTo(.7));}}
        [Test]public void ProcurementDiscountUsesExistingQuoteAndAutomationRequiresUnlock()
        {using(var sim=new SimulationManager(new CompanyData("Materials",100000),new GameTimeData(),42))using(var s=Session(sim)){Assert.Throws<InvalidOperationException>(()=>s.Research.SetAutomation(true,false));var supplier=s.Suppliers.Suppliers[0];var offer=supplier.Offers[0];double before=s.Procurement.Quote(supplier.Id,offer.MaterialId,10,ConstructionEmpire.Data.Procurement.ShippingMethod.Standard).Total;s.Research.System.Start("takeoffs",0);s.Research.System.OnTick(Tick(8));s.Research.System.Start("supplier-negotiation",8);s.Research.System.OnTick(Tick(20));double after=s.Procurement.Quote(supplier.Id,offer.MaterialId,10,ConstructionEmpire.Data.Procurement.ShippingMethod.Standard).Total;Assert.That(after,Is.EqualTo(Math.Round(before*.95,2)).Within(.01));}}
        [Test]public void ReorderingCountsIncomingOrdersAndHonorsReserve()
        {using(var sim=new SimulationManager(new CompanyData("Orders",100000),new GameTimeData(),42))using(var s=Session(sim,new ResearchSettings {AutomationEveryHours=1,DailyAutomationBudget=100000,CashReserve=5000})){var r=s.Research.System;r.Start("takeoffs",0);r.OnTick(Tick(8));r.Start("supplier-negotiation",8);r.OnTick(Tick(20));r.Start("reordering",20);r.OnTick(Tick(36));var p=s.Projects[0];p.Status=ProjectStatus.Won;p.ContractValue=100000;s.StartProject(p.Id);s.Research.SetAutomation(true,false);s.Research.OnTick(Tick(37));int count=s.Orders.Count;Assert.That(count,Is.GreaterThan(0));s.Research.OnTick(Tick(38));Assert.That(s.Orders.Count,Is.EqualTo(count));Assert.That(s.Finance.CashFlow.Cash,Is.GreaterThanOrEqualTo(5000));}}
        [Test]public void EquipmentEffectsApplyToFuelAndBreakdownMultipliers()
        {using(var sim=new SimulationManager(new CompanyData("Fleet",100000),new GameTimeData(),42))using(var s=Session(sim)){var r=s.Research.System;r.Start("takeoffs",0);r.OnTick(Tick(8));r.Start("crew-planning",8);r.OnTick(Tick(20));r.Start("fuel",20);r.OnTick(Tick(32));r.Start("reliability",32);r.OnTick(Tick(48));foreach(var e in s.Workforce.Equipment){Assert.That(e.EffectiveFuelPerHour,Is.EqualTo(e.FuelPerHour*.85));Assert.That(e.BreakdownRateMultiplier,Is.EqualTo(.8));}}}

        [Test]public void AutoSchedulingUsesCompanyWideBookingsAndSkipsAlreadyReservedCrew()
        {using(var sim=new SimulationManager(new CompanyData("Schedule",100000),new GameTimeData(),42))using(var s=Session(sim,new ResearchSettings {AutomationEveryHours=1})){var r=s.Research.System;r.Start("takeoffs",0);r.OnTick(Tick(8));r.Start("crew-planning",8);r.OnTick(Tick(20));r.Start("scheduling",20);r.OnTick(Tick(36));foreach(var p in s.Projects.Take(2)){p.Status=ProjectStatus.Won;p.ContractValue=100000;s.StartProject(p.Id);}s.Research.SetAutomation(false,true);s.Research.OnTick(Tick(37));Assert.That(s.Scheduling.Bookings.Count(x=>x.Open),Is.EqualTo(1));s.Research.OnTick(Tick(38));Assert.That(s.Scheduling.Bookings.Count(x=>x.Open),Is.EqualTo(1));}}
        [Test]public void AutoProcurementWillNotSpendReservedCash()
        {using(var sim=new SimulationManager(new CompanyData("Reserve",100000),new GameTimeData(),42))using(var s=Session(sim,new ResearchSettings {AutomationEveryHours=1,CashReserve=1000000})){var r=s.Research.System;r.Start("takeoffs",0);r.OnTick(Tick(8));r.Start("supplier-negotiation",8);r.OnTick(Tick(20));r.Start("reordering",20);r.OnTick(Tick(36));var p=s.Projects[0];p.Status=ProjectStatus.Won;p.ContractValue=100000;s.StartProject(p.Id);s.Research.SetAutomation(true,false);s.Research.OnTick(Tick(37));Assert.That(s.Orders,Is.Empty);Assert.That(s.Research.AutomationStatus,Does.Contain("reserve"));}}
    }
}
