using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Risk;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Risk;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class RiskSystemTests
    {
        private SimulationManager sim;private ManagementSession session;
        [SetUp]public void Setup()=>GameEvents.Clear();
        [TearDown]public void Cleanup(){session?.Dispose();sim?.Dispose();GameEvents.Clear();session=null;sim=null;}
        private static RiskSettings Quiet()=>new RiskSettings {WeatherShiftRate=0,BreakdownRate=0,AbsenceRate=0,SafetyRate=0,GroundDiscoveryRate=0,FatiguePerWorkHour=0,EquipmentWearPerHour=0,InspectionBaseFailure=0,InspectionVulnerabilityWeight=0};
        private ProjectData Start(string phaseId="excavation",double duration=20)
        {
            var config=new CrewEfficiencyConfiguration {SkillFloor=1,UnmatchedTradeMultiplier=1,ExperienceBonusPerYear=0,MaximumExperienceBonus=0,NoSupervisorMultiplier=1,SupervisorSkillBonus=0,MaximumFatiguePenalty=0};
            sim=new SimulationManager(new CompanyData("Risk tests",250000),new GameTimeData(),17,config);
            var p=SampleProjectDataFactory.CreateStarterProjects()[0];p.Site=new ProjectSiteProfile {Difficulty=0,Soil=0,Remoteness=0,AccessConstraint=0,Quality=1,Compliance=1};
            var phase=new ConstructionPhaseRuntimeData {Id=phaseId,Name=phaseId,DurationHours=duration,LaborHours=duration};
            var workforce=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment();
            session=new ManagementSession(sim,new[]{p},workforce,new Dictionary<string,List<ConstructionPhaseRuntimeData>>{{p.Id,new List<ConstructionPhaseRuntimeData>{phase}}},SampleProjectDataFactory.CreateRates(),SampleProjectDataFactory.CreateBiddingConfiguration(),SampleProjectDataFactory.CreateCompetitors(),ManagementSettings.CreateStarter(),riskSettings:Quiet());
            p=session.Projects[0];p.Status=ProjectStatus.Won;p.ContractValue=100000;session.StartProject(p.Id);sim.Construction.AssignCrew(p.Id,phaseId,session.Crew,workforce.Equipment);return p;
        }
        [Test]public void OneRiskFactorIncreasesScoreByItsWeightedContribution()
        {
            var settings=new RiskSettings();var factors=new RiskFactors();var low=ProjectRiskCalculator.Calculate(factors,settings);factors.SoilConditions=1;var higher=ProjectRiskCalculator.Calculate(factors,settings);
            Assert.That(higher.Score,Is.GreaterThan(low.Score));Assert.That(higher.Score-low.Score,Is.EqualTo(settings.Weights.SoilConditions/settings.Weights.Values().Sum()).Within(1e-10));
            var all=new RiskFactors {SiteDifficulty=1,DeadlineTightness=1,SoilConditions=1,Remoteness=1,SubcontractorReliability=1,MaterialShortageExposure=1,DesignComplexity=1,WeatherVolatility=1,CrewExperience=1};Assert.That(ProjectRiskCalculator.Calculate(all,settings).Level,Is.EqualTo(RiskLevel.Critical));
            factors.SoilConditions=double.NaN;Assert.Throws<ArgumentOutOfRangeException>(()=>ProjectRiskCalculator.Calculate(factors,settings));
        }
        [Test]public void CostOverrunRaisesFinalCostAndReducesProfitWithoutChangingProgress()
        {
            var p=SampleProjectDataFactory.CreateStarterProjects()[0];p.ContractValue=15000;var settings=new RiskSettings();var risk=ProjectRiskCalculator.Calculate(new RiskFactors(),settings);
            var baseline=ProjectForecastEngine.Calculate(p,10,50,5000,10000,10,1,0,risk,settings);var overrun=ProjectForecastEngine.Calculate(p,10,50,7000,10000,10,1,0,risk,settings);
            Assert.That(overrun.ExpectedFinalCost-baseline.ExpectedFinalCost,Is.EqualTo(2000));Assert.That(overrun.ExpectedProfit,Is.LessThan(baseline.ExpectedProfit));Assert.That(overrun.ExpectedCompletionHour,Is.EqualTo(baseline.ExpectedCompletionHour));
        }
        [Test]public void WeatherReplaysAndIndoorExposureIsLessThanExcavation()
        {
            var settings=new RiskSettings {WeatherShiftRate=1};var a=new WeatherSystem(new[]{"North"},new SeededRandom(37),settings);var b=new WeatherSystem(new[]{"North"},new SeededRandom(37),settings);
            for(int hour=1;hour<=100;hour++){a.OnTick(new TickContext(hour,24,7,28));b.OnTick(new TickContext(hour,24,7,28));Assert.That(a.Get("North").Weather,Is.EqualTo(b.Get("North").Weather));Assert.That(a.Get("North").Volatility,Is.EqualTo(b.Get("North").Volatility));}
            a.ForceShift("North",WeatherKind.Storm);Assert.That(a.Modifier("North","plumbing"),Is.GreaterThan(a.Modifier("North","excavation")));
        }
        [Test]public void WeatherFillsExistingProgressSlotAndRefreshesForecastImmediately()
        {
            var p=Start();var phase=session.Phases(p.Id)[0];sim.Advance(1);double clearProgress=phase.Progress;double clearFinish=session.Risks.Forecasts.Get(p.Id).ExpectedCompletionHour;
            session.Risks.Weather.ForceShift(p.Region,WeatherKind.Storm);Assert.That(session.Risks.Forecasts.Get(p.Id).ExpectedCompletionHour,Is.GreaterThan(clearFinish));sim.Advance(1);
            Assert.That(phase.Progress-clearProgress,Is.EqualTo(clearProgress*session.Risks.Settings.WeatherMultipliers[(int)WeatherKind.Storm]).Within(1e-8));
        }
        [Test]public void BreakdownBlocksRequiredEquipmentAndRepairRestoresWork()
        {
            var p=Start();var phase=session.Phases(p.Id)[0];phase.EquipmentRequirements.Add(new EquipmentRequirement(EquipmentType.SmallExcavator,1));var excavator=session.Workforce.Equipment.Single(x=>x.EquipmentType==EquipmentType.SmallExcavator);
            int count=0;GameEvents.Subscribe<EquipmentBreakdownEvent>(_=>count++);session.Risks.Events.Breakdown(p,excavator);sim.Advance(1);Assert.That(phase.Progress,Is.Zero);Assert.That(count,Is.EqualTo(1));
            var cost=session.Cost(p);session.Risks.Events.Repair(excavator);Assert.That(session.Cost(p),Is.GreaterThan(cost));sim.Advance(1);Assert.That(phase.Progress,Is.GreaterThan(0));
        }
        [Test]public void AbsenceExcludesWorkerCapacityAndReturnsAfterConfiguredDuration()
        {
            var p=Start();var worker=session.Workforce.Workers[0];var one=new CrewData {Id="one",Name="One",Members=new List<EmployeeData>{worker}};var source=new InternalCrewWorkSource(one);
            int events=0;GameEvents.Subscribe<WorkerAbsentEvent>(_=>events++);session.Risks.Events.Absent(p,worker,sim.Time.TotalHours);
            Assert.That(source.IsAvailable(1),Is.False);Assert.That(source.IsAvailable(session.Risks.Settings.AbsenceHours),Is.False);Assert.That(source.IsAvailable(session.Risks.Settings.AbsenceHours+1),Is.True);Assert.That(events,Is.EqualTo(1));
        }
        [Test]public void SafetyIncidentHasFinancialReputationAndWorkStoppageConsequences()
        {
            var p=Start();double cash=sim.Company.Reputation;session.Risks.Events.SafetyIncident(p,0);Assert.That(session.Cost(p),Is.EqualTo(session.Risks.Settings.SafetyInvestigationCost+session.Risks.Settings.SafetyInsuranceExcess));Assert.That(sim.Company.Reputation,Is.LessThan(cash));
            sim.Advance(1);Assert.That(session.Phases(p.Id)[0].Progress,Is.Zero);
        }
        [Test]public void FailedInspectionRequiresReworkAndCannotReleaseCompletionEarly()
        {
            var p=Start("inspection",1);var phase=session.Phases(p.Id)[0];int failed=0,complete=0;GameEvents.Subscribe<InspectionFailedEvent>(_=>failed++);GameEvents.Subscribe<ProjectCompletedEvent>(_=>complete++);
            session.Risks.Events.FailInspection(p,phase,0);Assert.That(phase.ReworkHoursRemaining,Is.EqualTo(session.Risks.Settings.InspectionReworkHours));sim.Advance(session.Risks.Settings.InspectionStopHours);Assert.That(complete,Is.Zero);
            sim.Advance((int)session.Risks.Settings.InspectionReworkHours);Assert.That(phase.Progress,Is.Zero);sim.Advance(1);Assert.That(complete,Is.EqualTo(1));Assert.That(failed,Is.EqualTo(1));Assert.That(p.Status,Is.EqualTo(ProjectStatus.Complete));
        }
        [Test]public void NaturalInspectionFailureIsSeededAndRetainsEarnedProgress()
        {
            var p=Start("inspection",1);session.Risks.Settings.InspectionBaseFailure=1;session.Risks.Settings.MinimumRiskFrequency=1;var phase=session.Phases(p.Id)[0];sim.Advance(1);
            Assert.That(phase.IsComplete,Is.False);Assert.That(phase.ReworkHoursRemaining,Is.GreaterThan(0));Assert.That(session.Finance.GetContract(p.Id).Progress,Is.Zero);
        }
        [Test]public void RiskFrequencyScalesProbabilityAndZeroRatesDoNotTrigger()
        {
            var s=new RiskSettings();Assert.That(RandomEventSystem.Probability(.1,.9,.7,1,s),Is.GreaterThan(RandomEventSystem.Probability(.1,.1,.7,1,s)));Assert.That(RandomEventSystem.Probability(0,1,1,100,s),Is.Zero);
        }
        [Test]public void GroundDiscoveryLowersSiteProductivityAndAddsCostOnce()
        {
            var p=Start();double before=session.Risks.Sites.Modifier(p,1);session.Risks.Events.DiscoverGround(p,0);Assert.That(session.Risks.Sites.Modifier(p,1),Is.Zero);Assert.That(session.Risks.Sites.Modifier(p,100),Is.LessThan(before));Assert.That(session.Cost(p),Is.EqualTo(session.Risks.Settings.GroundRemediationCost));Assert.Throws<InvalidOperationException>(()=>session.Risks.Events.DiscoverGround(p,0));
        }
        [Test]public void ProactiveWarningsResolveAndDoNotDuplicateReactiveEvents()
        {
            var p=Start();var machine=session.Workforce.Equipment[0];machine.Condition=.1;session.Risks.Forecasts.Refresh(p.Id);Assert.That(session.Risks.Alerts.Active.Any(x=>x.EntityId==machine.Id),Is.True);
            int count=session.Risks.Alerts.Active.Count;session.Risks.Forecasts.Refresh(p.Id);Assert.That(session.Risks.Alerts.Active.Count,Is.EqualTo(count));session.Risks.Events.Repair(machine);Assert.That(session.Risks.Alerts.Active.Any(x=>x.EntityId==machine.Id),Is.False);
        }
        [Test]public void FinanceEventRecomputesForecastWithoutAdvancingClock()
        {
            var p=Start();double before=session.Risks.Forecasts.Get(p.Id).ExpectedFinalCost;session.Finance.AccrueExpense(p.Id,1000,ConstructionEmpire.Data.Finance.FinancialCategory.Other,"overrun","Unexpected cost",0);Assert.That(session.Risks.Forecasts.Get(p.Id).ExpectedFinalCost,Is.EqualTo(before+1000).Within(.01));Assert.That(sim.Time.TotalHours,Is.Zero);
        }
    }
}
