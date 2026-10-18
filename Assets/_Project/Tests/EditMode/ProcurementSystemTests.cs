using System;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Data.Procurement;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Finance;
using ConstructionEmpire.Systems.Procurement;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class ProcurementSystemTests
    {
        private SimulationManager sim;private FinanceSystem finance;private SupplierSystem suppliers;private MaterialProcurementSystem procurement;private MaterialInventoryData inventory;
        [SetUp]public void Setup()
        {
            GameEvents.Clear();sim=new SimulationManager(new CompanyData("Procurement",100000),new GameTimeData(),123);
            finance=new FinanceSystem(sim.Company,sim.Time,new FinanceSettings {OfficeCostPerDay=0,InsurancePremium=0,TaxRate=0});sim.Register(finance);
            var scheduler=new ScheduledEventQueue();sim.Register(scheduler);var settings=new ProcurementSettings();
            suppliers=new SupplierSystem(SampleSupplierDataFactory.CreateStarterSuppliers(),sim.Random,settings);sim.Register(suppliers);inventory=new MaterialInventoryData();
            procurement=new MaterialProcurementSystem(suppliers,inventory,finance,scheduler,sim.Time,sim.Random,settings);
        }
        [TearDown]public void Cleanup(){procurement.Dispose();finance.Dispose();sim.Dispose();GameEvents.Clear();}
        [Test]public void ExpeditedDeliveryIsEarlierAndMoreExpensive()
        {
            var standard=procurement.PlaceOrder("p","supplier-10","concrete",10,ShippingMethod.Standard);
            var express=procurement.PlaceOrder("p","supplier-10","concrete",10,ShippingMethod.Expedited);
            Assert.That(express.DueHour,Is.LessThan(standard.DueHour));Assert.That(express.Cost,Is.GreaterThan(standard.Cost));
            sim.Advance(1);Assert.That(inventory.GetQuantity("concrete"),Is.Zero);sim.Advance(1);Assert.That(express.Delivered,Is.True);Assert.That(standard.Delivered,Is.False);Assert.That(inventory.GetQuantity("concrete"),Is.EqualTo(10));
            sim.Advance(2);Assert.That(standard.Delivered,Is.True);Assert.That(inventory.GetQuantity("concrete"),Is.EqualTo(20));
        }
        [Test]public void FailedReliabilityRollSignalsDelayAndDoesNotDeliverUntilRevisedHour()
        {
            suppliers.Get("supplier-10").Reliability=0;int delayed=0,delivered=0;
            GameEvents.Subscribe<MaterialDelayedEvent>(_=>delayed++);GameEvents.Subscribe<MaterialDeliveredEvent>(_=>delivered++);
            var order=procurement.PlaceOrder("p","supplier-10","concrete",10,ShippingMethod.Standard);sim.Advance(4);
            Assert.That(delayed,Is.EqualTo(1));Assert.That(delivered,Is.Zero);Assert.That(inventory.GetQuantity("concrete"),Is.Zero);Assert.That(order.DueHour,Is.EqualTo(28));
            for(int i=0;i<23;i++)sim.Advance(1);Assert.That(delivered,Is.Zero);sim.Advance(1);Assert.That(delivered,Is.EqualTo(1));sim.Advance(5);Assert.That(delivered,Is.EqualTo(1));
            Assert.That(finance.Expenses,Is.EqualTo((decimal)order.Cost));
        }
        [Test]public void BulkDiscountAndCapacityAreEnforcedWithoutDuplicateCosts()
        {
            var q=procurement.Quote("supplier-10","steel",100,ShippingMethod.Standard);Assert.That(q.Discount,Is.EqualTo(.1));
            Assert.That(q.Total,Is.EqualTo(Math.Round(q.UnitPrice*100*.9,2)));
            procurement.PlaceOrder("p","supplier-10","steel",10000,ShippingMethod.Standard);
            Assert.Throws<InvalidOperationException>(()=>procurement.PlaceOrder("p","supplier-10","steel",1,ShippingMethod.Standard));Assert.That(procurement.Orders.Count,Is.EqualTo(1));
        }
        [Test]public void SupplierPricesDriftIndependentlyWithinBoundsAndReplayFromSeed()
        {
            var a=new SupplierSystem(SampleSupplierDataFactory.CreateStarterSuppliers(),new SeededRandom(4),new ProcurementSettings());
            var b=new SupplierSystem(SampleSupplierDataFactory.CreateStarterSuppliers(),new SeededRandom(4),new ProcurementSettings());
            for(int day=1;day<=300;day++){var tick=new TickContext(day*24,24,7,28);a.OnTick(tick);b.OnTick(tick);}
            CollectionAssert.AreEqual(a.Suppliers.SelectMany(x=>x.Offers).Select(x=>x.CurrentPrice),b.Suppliers.SelectMany(x=>x.Offers).Select(x=>x.CurrentPrice));
            foreach(var offer in a.Suppliers.SelectMany(x=>x.Offers))Assert.That(offer.CurrentPrice,Is.InRange(offer.BasePrice*.6,offer.BasePrice*1.6));
            Assert.That(a.Offer("supplier-1","concrete").CurrentPrice/a.Offer("supplier-1","concrete").BasePrice,Is.Not.EqualTo(a.Offer("supplier-2","concrete").CurrentPrice/a.Offer("supplier-2","concrete").BasePrice));
        }
        [Test]public void DeliveredInventoryFeedsConstructionAndIdleWagesAccrueDuringMaterialStall()
        {
            var project=ConstructionEmpire.Data.Bidding.SampleProjectDataFactory.CreateStarterProjects()[0];project.Status=ConstructionEmpire.Data.Bidding.ProjectStatus.Won;
            var phase=new ConstructionPhaseRuntimeData {Id="phase",Name="Phase",DurationHours=2,LaborHours=2,Cost=0};phase.MaterialRequirements.Add(new MaterialQuantity("concrete",10));
            var worker=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment().Workers[0];worker.Productivity=1;
            var crew=new CrewData {Id="crew",Name="Crew",Members=new System.Collections.Generic.List<EmployeeData>{worker}};
            var config=new CrewEfficiencyConfiguration {SkillFloor=1,UnmatchedTradeMultiplier=1,ExperienceBonusPerYear=0,MaximumExperienceBonus=0,NoSupervisorMultiplier=1,SupervisorSkillBonus=0,MaximumFatiguePenalty=0};
            var engine=new ConstructionEmpire.Systems.Construction.ProjectProgressEngine(config);sim.Register(engine);
            engine.StartProject(project,new[]{phase},inventory);engine.AssignCrew(project.Id,phase.Id,crew,Array.Empty<EquipmentRuntimeData>());
            var order=procurement.PlaceOrder(project.Id,"supplier-10","concrete",10,ShippingMethod.Standard);
            sim.Advance(3);Assert.That(phase.Progress,Is.Zero);Assert.That(phase.AccruedCost,Is.EqualTo(crew.CostPerHour*3));
            sim.Advance(1);Assert.That(order.Delivered,Is.True);Assert.That(phase.Progress,Is.GreaterThan(0));Assert.That(inventory.GetQuantity("concrete"),Is.LessThan(10));
            sim.Advance(1);Assert.That(phase.IsComplete,Is.True);Assert.That(inventory.GetQuantity("concrete"),Is.Zero.Within(.000001));
        }
        [Test]public void StarterCatalogHasTenSuppliersAndMultipleOptionsPerMaterial()
        {Assert.That(suppliers.Suppliers.Count,Is.EqualTo(10));foreach(var m in ManagementSettings.CreateStarter().Materials)Assert.That(suppliers.ForMaterial(m.Id).Count(),Is.GreaterThanOrEqualTo(3));}
    }
}
