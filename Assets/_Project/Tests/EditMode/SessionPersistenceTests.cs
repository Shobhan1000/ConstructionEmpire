
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Persistence;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class SessionPersistenceTests
    {
        [SetUp]public void Setup()=>GameEvents.Clear();
        [TearDown]public void Cleanup()=>GameEvents.Clear();
        private static SessionStartData Origin()
        {
            var projects=SampleProjectDataFactory.CreateStarterProjects();
            return new SessionStartData {Company=new CompanyData("Save Test",250000),Clock=new GameTimeData(),Seed=42,Projects=projects,Workforce=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment(),Phases=projects.ToDictionary(x=>x.Id,x=>SampleRenovationPhaseFactory.CreateHouseRenovationPhases()),Rates=SampleProjectDataFactory.CreateRates(),Bidding=SampleProjectDataFactory.CreateBiddingConfiguration(),Competitors=SampleProjectDataFactory.CreateCompetitors(),Management=ManagementSettings.CreateStarter(),Market=new MarketSettings {CompetitorBidChance=0,OpportunityBaseCount=0}};
        }
        private static void Close(ManagementSession s){s.Dispose();s.Simulation.Dispose();}
        private static void Advance(ManagementSession s,int hours){s.Simulation.Time.Speed=SimulationSpeed.Normal;for(int i=0;i<hours;i++)s.Simulation.Advance(1);}
        private static void Finish(SessionReplay r){int guard=0;while(!r.Step(17))if(++guard>10000)Assert.Fail("Replay did not finish.");}
        [Test]public void FrozenConfigurationRoundTripsAndDoesNotAliasRuntime()
        {
            var origin=Origin();var copy=SessionJson.Copy(origin);Assert.That(copy.Workforce.Workers.Count,Is.EqualTo(6));
            var s=SessionFactory.Create(origin);
            try{origin.Company.Rename("Changed authoring data");s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.RenameCompany,Text="Live name"});Assert.That(s.Commands.Capture().Initial.Company.CompanyName,Is.EqualTo("Save Test"));}
            finally{Close(s);}
        }
        [Test]public void OrdersLoanResearchAndRandomContinuationSurviveReplayWithoutLeakingEvents()
        {
            var s=SessionFactory.Create(Origin());
            try
            {
                s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.DrawLoan,Amount=10000,Start=12,Text="Fleet",Value=62000});
                s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.StartResearch,Id="takeoffs"});
                s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.OrderMaterial,Id=s.Projects[0].Id,OtherId="concrete",Amount=4});
                Advance(s,2);var save=s.Commands.Capture();int leaked=0;using(var listeners=new EventSubscriptions())
                {
                    listeners.Subscribe<BalanceChangedEvent>(_=>leaked++);
                    using(var replay=new SessionReplay(save))
                    {
                        Finish(replay);Assert.That(leaked,Is.Zero);Assert.That(replay.Session.Orders.Single().Delivered,Is.False);
                        Assert.That(SessionStateProof.Compute(replay.Session),Is.EqualTo(save.StateProof));
                        Advance(s,700);using(replay.ActivateForVerification())Advance(replay.Session,700);
                        Assert.That(SessionStateProof.Compute(replay.Session),Is.EqualTo(SessionStateProof.Compute(s)));
                        Assert.That(replay.Session.Research.System.IsUnlocked("takeoffs"),Is.True);
                        Assert.That(replay.Session.Finance.Banking.Debt,Is.LessThan(10000));
                    }
                }
            }
            finally{Close(s);}
        }
        [Test]public void ActivePhaseAndPendingBookingResumeWithoutDuplicateCosts()
        {
            var origin=Origin();origin.Projects[0].Status=ProjectStatus.Won;origin.Projects[0].ContractValue=44000;
            var s=SessionFactory.Create(origin);
            try
            {
                var p=s.Projects[0];
                s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.StartProject,Id=p.Id});
                var phase=s.Phases(p.Id).First(x=>x.PrerequisiteIds.Count==0);
                foreach(var m in phase.MaterialRequirements)s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.OrderMaterial,Id=p.Id,OtherId=m.MaterialId,Amount=m.Quantity});
                s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.ReserveCrew,Id=p.Id,OtherId=phase.Id,ThirdId=s.Crew.Id,Start=4,End=40,EquipmentIds=s.Workforce.Equipment.Select(x=>x.Id).ToList()});
                Advance(s,2);using(var replay=new SessionReplay(s.Commands.Capture()))
                {Finish(replay);Advance(s,12);using(replay.ActivateForVerification())Advance(replay.Session,12);Assert.That(SessionStateProof.Compute(replay.Session),Is.EqualTo(SessionStateProof.Compute(s)));Assert.That(s.Progress(p),Is.GreaterThan(0));}
            }
            finally{Close(s);}
        }
        [Test]public void FailedPlayerAttemptIsReproducedAndDoesNotPreventSaving()
        {
            var s=SessionFactory.Create(Origin());
            try{Assert.Throws<InvalidOperationException>(()=>s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.StartResearch,Id="ai-estimating"}));using(var r=new SessionReplay(s.Commands.Capture())){Finish(r);Assert.That(r.Session.Research.System.ActiveId,Is.Null);}}
            finally{Close(s);}
        }
        [Test]public void CorruptFileIsRejectedAndAtomicReplacementKeepsPreviousBackup()
        {
            string dir=Path.Combine(Path.GetTempPath(),"ConstructionEmpire-save-test-"+Guid.NewGuid().ToString("N"));string path=Path.Combine(dir,"slot.json");var s=SessionFactory.Create(Origin());
            try
            {SessionSaveService.Save(path,s.Commands.Capture());string first=File.ReadAllText(path);Advance(s,1);SessionSaveService.Save(path,s.Commands.Capture());Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(first));Assert.That(SessionSaveService.Load(path).Clock.TotalHours,Is.EqualTo(1));File.WriteAllText(path,"{}");Assert.Throws<SerializationException>(()=>SessionSaveService.Load(path));Assert.That(SessionSaveService.Load(path+".bak").Clock.TotalHours,Is.Zero);}
            finally{Close(s);if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test]public void UnknownVersionAndUnorderedCommandsAreRejectedBeforeReconstruction()
        {var s=SessionFactory.Create(Origin());try{var save=s.Commands.Capture();save.Version++;Assert.Throws<NotSupportedException>(()=>new SessionReplay(save));save.Version--;save.Commands.Add(new SessionCommandData {Kind=SessionCommandKind.Refuel,Hour=-1});Assert.Throws<InvalidDataException>(()=>new SessionReplay(save));}finally{Close(s);}}
        [Test]public void InvalidProofCannotReplaceOrMutateLiveSession()
        {var s=SessionFactory.Create(Origin());try{string before=SessionStateProof.Compute(s);var save=s.Commands.Capture();save.StateProof="invalid";using(var replay=new SessionReplay(save))Assert.Throws<InvalidDataException>(()=>Finish(replay));Assert.That(SessionStateProof.Compute(s),Is.EqualTo(before));}finally{Close(s);}}
        [Test]public void EventDomainsKeepOwnedUnsubscribeCorrectAfterAttachment()
        {int oldCalls=0,newCalls=0;using(var old=new EventSubscriptions()){old.Subscribe<int>(_=>oldCalls++);var domain=new GameEvents.Domain();var next=new EventSubscriptions();using(domain.Activate()){next.Subscribe<int>(_=>newCalls++);GameEvents.Publish(1);}Assert.That(oldCalls,Is.Zero);Assert.That(newCalls,Is.EqualTo(1));domain.Attach();GameEvents.Publish(1);Assert.That(oldCalls,Is.EqualTo(1));Assert.That(newCalls,Is.EqualTo(2));next.Dispose();GameEvents.Publish(1);Assert.That(newCalls,Is.EqualTo(2));}}

        [Test]public void EquipmentPurchaseIsCapitalAndRentalChargesStopOnReturn()
        {
            var session=SessionFactory.Create(Origin());try
            {
                var template=session.Development.Settings.EquipmentCatalog[0];double profit=session.Profit;decimal cash=session.Finance.CashFlow.Cash;
                session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.AcquireEquipment,Id=template.DefinitionId});
                Assert.That(session.Profit,Is.EqualTo(profit));Assert.That(session.Finance.CashFlow.Cash,Is.EqualTo(cash-(decimal)template.PurchasePrice));
                session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.AcquireEquipment,Id=template.DefinitionId,Flag=true});
                var rental=session.Workforce.Equipment.Last();Advance(session,24);
                Assert.That(session.Finance.Ledger.Count(x=>x.Category==ConstructionEmpire.Data.Finance.FinancialCategory.Rental),Is.EqualTo(4));
                session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.ReleaseEquipment,Id=rental.Id});int count=session.Finance.Ledger.Count(x=>x.Category==ConstructionEmpire.Data.Finance.FinancialCategory.Rental);Advance(session,24);
                Assert.That(session.Finance.Ledger.Count(x=>x.Category==ConstructionEmpire.Data.Finance.FinancialCategory.Rental),Is.EqualTo(count));
                using(var replay=new SessionReplay(session.Commands.Capture()))Finish(replay);
            }finally{Close(session);}
        }
        [Test]public void CrewPolicyBlocksOffShiftAndWeekendWorkAndSurvivesSave()
        {
            var session=SessionFactory.Create(Origin());try
            {
                session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.SetWorkPolicy,Id=session.Crew.Id,Flag=true});
                Assert.That(session.Crew.IsOnShift(7),Is.False);Assert.That(session.Crew.IsOnShift(8),Is.True);Assert.That(session.Crew.IsOnShift(19),Is.True);Assert.That(session.Crew.IsOnShift(20),Is.False);Assert.That(session.Crew.IsOnShift(5*24+8),Is.False);
                using(var replay=new SessionReplay(session.Commands.Capture())){Finish(replay);Assert.That(replay.Session.Crew.OvertimeMultiplier,Is.EqualTo(1.5));}
            }finally{Close(session);}
        }
        [Test]public void PromotionRequiresCompletedWorkRevenueAndReputation()
        {
            var settings=new ConstructionEmpire.Data.Company.DevelopmentSettings();
            Assert.That(CompanyDevelopmentSystem.CalculateLevel(settings,3,100000,49),Is.EqualTo(1));
            Assert.That(CompanyDevelopmentSystem.CalculateLevel(settings,3,99999,50),Is.EqualTo(1));
            Assert.That(CompanyDevelopmentSystem.CalculateLevel(settings,3,100000,50),Is.EqualTo(2));
            Assert.That(CompanyDevelopmentSystem.CalculateLevel(settings,60,10000000,80),Is.EqualTo(5));
        }
        [Test]public void InvalidNumericCommandDoesNotPoisonFutureSaves()
        {var session=SessionFactory.Create(Origin());try{Assert.Throws<ArgumentException>(()=>session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.SubmitBid,Amount=double.NaN}));using(var replay=new SessionReplay(session.Commands.Capture()))Finish(replay);}finally{Close(session);}}

        [Test]public void AcceptedScopeAmendsRequirementsAndDeadlineWithoutImmediateRevenue()
        {
            var origin=Origin();origin.Projects[0].Status=ProjectStatus.Won;origin.Projects[0].ContractValue=50000;var session=SessionFactory.Create(origin);
            try
            {
                var p=session.Projects[0];session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.StartProject,Id=p.Id});
                var o=(ConstructionEmpire.Data.Company.ChangeOrderData)session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.ProposeChangeOrder,Id=p.Id});
                var phase=session.Phases(p.Id).Single(x=>x.Id==o.PhaseId);double hours=phase.DurationHours;long deadline=p.DeadlineHour;decimal earned=session.Finance.Revenue;
                session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.DecideChangeOrder,Id=o.Id,Flag=true});
                Assert.That(phase.DurationHours,Is.EqualTo(hours*(1+o.ScopeFraction)));Assert.That(p.DeadlineHour,Is.EqualTo(deadline+o.ExtensionHours));Assert.That(session.Finance.Revenue,Is.EqualTo(earned));
                Assert.That(session.Finance.GetContract(p.Id).ContractValue,Is.EqualTo(50000+(decimal)o.OfferedPrice));
                Assert.Throws<InvalidOperationException>(()=>session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.DecideChangeOrder,Id=o.Id,Flag=true}));
                using(var replay=new SessionReplay(session.Commands.Capture()))Finish(replay);
            }finally{Close(session);}
        }
        [Test]public void DeclinedScopeLeavesCostAndScheduleUntouched()
        {
            var origin=Origin();origin.Projects[0].Status=ProjectStatus.Won;origin.Projects[0].ContractValue=50000;var session=SessionFactory.Create(origin);
            try{var p=session.Projects[0];session.StartProject(p.Id);var o=session.ChangeOrders.Propose(p.Id);double duration=session.Phases(p.Id).Sum(x=>x.DurationHours);long deadline=p.DeadlineHour;session.ChangeOrders.Decide(o.Id,false);Assert.That(p.ContractValue,Is.EqualTo(50000));Assert.That(p.DeadlineHour,Is.EqualTo(deadline));Assert.That(session.Phases(p.Id).Sum(x=>x.DurationHours),Is.EqualTo(duration));}finally{Close(session);}
        }
        [Test]public void InsuranceClaimRecognizesReceivableBeforeCashAndCannotBeDuplicated()
        {
            var session=SessionFactory.Create(Origin());try
            {
                var f=session.Finance;var insurance=f.Insurance;decimal cash=f.CashFlow.Cash;
                decimal payout=insurance.Claim(ConstructionEmpire.Systems.Finance.InsuranceType.GeneralLiability,"test-incident",null,10000);
                Assert.That(payout,Is.GreaterThan(0));Assert.That(f.CashFlow.Cash,Is.EqualTo(cash));Assert.That(f.AccountsReceivable,Is.EqualTo(payout));
                Assert.That(insurance.Claim(ConstructionEmpire.Systems.Finance.InsuranceType.GeneralLiability,"test-incident",null,10000),Is.Zero);
                Assert.That(insurance.Claim(ConstructionEmpire.Systems.Finance.InsuranceType.Equipment,"uninsured-incident",null,10000),Is.Zero);
                Advance(session,f.Settings.InsuranceClaimDelayDays*f.HoursPerDay);Assert.That(f.AccountsReceivable,Is.Zero);
                Assert.That(f.Ledger.Count(x=>x.Kind==ConstructionEmpire.Data.Finance.LedgerKind.CashReceipt && x.Category==ConstructionEmpire.Data.Finance.FinancialCategory.Insurance),Is.EqualTo(1));
            }finally{Close(session);}
        }

        [Test]public void OneYearWithThousandsOfLedgerEntriesReplaysExactly()
        {
            var origin=Origin();origin.Company=new CompanyData("Long running company",10000000);
            var s=SessionFactory.Create(origin);var timer=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                for(int i=0;i<10;i++)s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.AcquireEquipment,Id=s.Development.Settings.EquipmentCatalog[0].DefinitionId,Flag=true});
                Advance(s,365*24);long simulationMs=timer.ElapsedMilliseconds;timer.Restart();
                Assert.That(s.Finance.Ledger.Count,Is.GreaterThan(3000));var save=s.Commands.Capture();
                using(var replay=new SessionReplay(save)){Finish(replay);Assert.That(SessionStateProof.Compute(replay.Session),Is.EqualTo(save.StateProof));}
                TestContext.WriteLine("One game year: "+simulationMs+" ms; save and replay: "+timer.ElapsedMilliseconds+" ms; ledger entries: "+s.Finance.Ledger.Count+"; save characters: "+SessionJson.Serialize(save).Length);
            }finally{Close(s);}
        }

        [Test]public void StarterRenovationCanFinishReceivePaymentAndReloadThroughPlayerCommands()
        {
            var origin=Origin();origin.Projects[0].Status=ProjectStatus.Won;origin.Projects[0].ContractValue=50000;
            string[] specialistPhases={"electrical","plumbing","hvac","roofing","landscaping","framing"};
            for(int i=0;i<specialistPhases.Length;i++){var phase=origin.Phases[origin.Projects[0].Id].Single(x=>x.Id==specialistPhases[i]);phase.RequiresSubcontractor=true;phase.SubcontractorSpecialty=(ConstructionEmpire.Data.Subcontractors.SubcontractorSpecialty)i;}
            var planning=SessionFactory.Create(origin);
            origin.Projects[0].ContractValue=(float)planning.Estimation.Estimate(planning.Projects[0],.12,.08).BidPrice;Close(planning);
            var s=SessionFactory.Create(origin);
            try
            {
                var p=s.Projects[0];s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.SetWorkPolicy,Id=s.Crew.Id});s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.StartProject,Id=p.Id});
                foreach(var group in s.Phases(p.Id).SelectMany(x=>x.MaterialRequirements).GroupBy(x=>x.MaterialId))
                    s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.OrderMaterial,Id=p.Id,OtherId=group.Key,Amount=group.Sum(x=>x.Quantity)*1.2});
                for(int h=0;h<2000 && p.Status!=ProjectStatus.Complete;h++)
                {
                    foreach(var machine in s.Workforce.Equipment)
                    {
                        if(machine.Status==EquipmentStatus.Broken)s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.Repair,Id=machine.Id});
                        if(machine.CurrentFuel<machine.FuelCapacity*.2)s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.Refuel,Id=machine.Id});
                    }
                    foreach(var phase in s.Phases(p.Id).Where(x=>!x.IsComplete && x.PrerequisiteIds.All(id=>s.Phases(p.Id).Single(y=>y.Id==id).IsComplete)))
                    {
                        if(s.Scheduling.Bookings.Any(x=>x.Open && x.Data.ProjectId==p.Id && x.Data.PhaseId==phase.Id))continue;
                        if(phase.RequiresSubcontractor)
                        {
                            s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.RequestQuotes,Id=p.Id,OtherId=phase.Id});
                            var quote=s.Subcontractors.GetQuotes(p.Id,phase.Id).OrderBy(x=>x.Price).First();
                            s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.AwardSubcontractor,Id=p.Id,OtherId=phase.Id,ThirdId=quote.Id});
                        }
                        else if(s.Crew.CurrentProjectId==null && !s.Scheduling.Bookings.Any(x=>x.Open && x.Source.Id==s.Crew.Id))
                            s.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.AssignCrew,Id=p.Id,OtherId=phase.Id});
                    }
                    Advance(s,1);
                }
                Assert.That(p.Status,Is.EqualTo(ProjectStatus.Complete),"The starter project must be completable with normal player commands.");
                Advance(s,s.Finance.Settings.FinalPaymentDelayDays*s.Finance.HoursPerDay+1);
                Assert.That(s.Finance.Ledger.Any(x=>x.ProjectId==p.Id && x.Kind==ConstructionEmpire.Data.Finance.LedgerKind.CashReceipt && x.Category==ConstructionEmpire.Data.Finance.FinancialCategory.FinalPayment),Is.True);
                using(var replay=new SessionReplay(s.Commands.Capture()))Finish(replay);
                Assert.That(s.Finance.Forecast(p.Id).ForecastProfit,Is.GreaterThan(0),"The planned starter bid should cover the complete authored scope.");
                TestContext.WriteLine("Starter renovation completed at hour "+s.Finance.GetContract(p.Id).CompletionHour+"; project profit "+s.Finance.Forecast(p.Id).ForecastProfit+"; liquid cash "+s.Finance.CashFlow.Cash);
            }finally{Close(s);}
        }
    }
}
