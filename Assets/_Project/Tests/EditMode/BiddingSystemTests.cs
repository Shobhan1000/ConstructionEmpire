using System;
using System.Collections.Generic;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Bidding;
using ConstructionEmpire.Systems.Persistence;
using ConstructionEmpire.Systems.Randomness;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class BiddingSystemTests
    {
        [SetUp] public void SetUp() => GameEvents.Clear();
        [TearDown] public void TearDown() => GameEvents.Clear();
        private static BidderCapabilities Player(float reputation=50) => new BidderCapabilities {
            Reputation=reputation,Experience=.6f,Quality=.7f,ClientRelationship=.5f,Certifications=.7f,MarketFit=.5f };
        private static BiddingSystem Create(int seed=42) => new BiddingSystem(SampleProjectDataFactory.CreateBiddingConfiguration(),new SeededRandom(seed));

        [Test]
        public void Estimate_ComposesEveryLineThenProfitAndRiskOnBaseCost()
        {
            var project=SampleProjectDataFactory.CreateStarterProjects()[0];
            project.Requirements=new RequirementProfile { LaborHours=10,MaterialUnits=20,EquipmentHours=3,SubcontractorHours=4 };
            project.EstimatedDurationHours=5;
            var rates=new EstimationRates { LaborPerHour=30,MaterialPerUnit=5,EquipmentPerHour=40,SubcontractorPerHour=50,OverheadPerHour=10,OverheadFraction=.1f };
            var estimate=new EstimationSystem(rates).Estimate(project,.2,.1);
            Assert.That(estimate.Labor,Is.EqualTo(300)); Assert.That(estimate.Materials,Is.EqualTo(100));
            Assert.That(estimate.Equipment,Is.EqualTo(120)); Assert.That(estimate.Subcontractors,Is.EqualTo(200));
            Assert.That(estimate.Overhead,Is.EqualTo(122).Within(.0001));
            Assert.That(estimate.EstimatedCost,Is.EqualTo(842).Within(.0001));
            Assert.That(estimate.DesiredProfit,Is.EqualTo(168.4).Within(.0001));
            Assert.That(estimate.RiskMargin,Is.EqualTo(84.2).Within(.0001));
            Assert.That(estimate.BidPrice,Is.EqualTo(1094.6).Within(.0001));
        }

        [Test]
        public void LowerPriceAndHigherReputation_StrictlyIncreaseProbability()
        {
            var p=SampleProjectDataFactory.CreateStarterProjects()[0];var competitors=SampleProjectDataFactory.CreateCompetitors();var system=Create();
            double baseline=system.CalculateWinProbability(p,48000,240,Player(),competitors);
            Assert.That(system.CalculateWinProbability(p,40000,240,Player(),competitors),Is.GreaterThan(baseline));
            Assert.That(system.CalculateWinProbability(p,56000,240,Player(),competitors),Is.LessThan(baseline));
            Assert.That(system.CalculateWinProbability(p,48000,240,Player(75),competitors),Is.GreaterThan(baseline));
            Assert.That(system.CalculateWinProbability(p,48000,240,Player(25),competitors),Is.LessThan(baseline));
            Assert.That(baseline,Is.InRange(0d,1d));
        }

        [Test]
        public void SeedAndInputs_ReplayIdenticalOutcomes()
        {
            var first=Create(789);var second=Create(789);var rivals=SampleProjectDataFactory.CreateCompetitors();
            for(int i=0;i<30;i++)
            {
                var a=SampleProjectDataFactory.CreateStarterProjects()[0];var b=a.Copy();
                var x=first.SubmitBid(a,"bid-"+i,42000,240,Player(),rivals,0);
                var y=second.SubmitBid(b,"bid-"+i,42000,240,Player(),rivals,0);
                Assert.That(y.Won,Is.EqualTo(x.Won));Assert.That(y.NarrowlyLost,Is.EqualTo(x.NarrowlyLost));
                Assert.That(b.Status,Is.EqualTo(a.Status));Assert.That(b.ContractValue,Is.EqualTo(a.ContractValue));
            }
        }

        [Test]
        public void WonBid_PublishesSubmittedThenWon_AndCannotBeSubmittedAgain()
        {
            var project=SampleProjectDataFactory.CreateStarterProjects()[0];var system=Create();var calls=new List<string>();
            GameEvents.Subscribe<BidSubmittedEvent>(e=> { calls.Add("submitted");Assert.That(project.Status,Is.EqualTo(ProjectStatus.Bidding));Assert.That(e.Amount,Is.EqualTo(42000)); });
            GameEvents.Subscribe<BidWonEvent>(e=> { calls.Add("won");Assert.That(e.ProjectId,Is.EqualTo(project.Id));Assert.That(project.Status,Is.EqualTo(ProjectStatus.Won)); });
            GameEvents.Subscribe<BidLostEvent>(e=>Assert.Fail("Unexpected loss"));
            var result=system.SubmitBid(project,"B1",42000,240,Player(),new CompetitorCompanyData[0],0);
            Assert.That(result.Won,Is.True);Assert.That(project.ContractValue,Is.EqualTo(42000));
            Assert.That(calls,Is.EqualTo(new[] { "submitted","won" }));
            Assert.Throws<InvalidOperationException>(()=>system.SubmitBid(project,"B2",42000,240,Player(),new CompetitorCompanyData[0],0));
        }

        [Test]
        public void LostBid_PublishesLostWithoutContractValue()
        {
            var config=SampleProjectDataFactory.CreateBiddingConfiguration();config.ScoreSensitivity=1000;
            var system=new BiddingSystem(config,new SeededRandom(42));var p=SampleProjectDataFactory.CreateStarterProjects()[0];
            var rival=SampleProjectDataFactory.CreateCompetitors()[1];rival.Reputation=100;rival.Experience=1;rival.Quality=1;
            rival.ClientRelationship=1;rival.Certifications=1;rival.MarketFit=1;rival.PriceMultiplier=.1f;rival.DurationMultiplier=.1f;
            int lost=0;GameEvents.Subscribe<BidLostEvent>(e=> { lost++;Assert.That(e.BidId,Is.EqualTo("lose")); });
            var result=system.SubmitBid(p,"lose",1000000,240,new BidderCapabilities(),new[] { rival },0);
            Assert.That(result.Won,Is.False);Assert.That(lost,Is.EqualTo(1));Assert.That(p.ContractValue,Is.Zero);Assert.That(p.Status,Is.EqualTo(ProjectStatus.Lost));
        }

        [Test]
        public void InvalidBid_DoesNotChangeStateOrConsumeRandomDraw()
        {
            var rng=new SeededRandom(42);var untouched=new SeededRandom(42);var system=new BiddingSystem(SampleProjectDataFactory.CreateBiddingConfiguration(),rng);
            var p=SampleProjectDataFactory.CreateStarterProjects()[0];
            Assert.Throws<ArgumentOutOfRangeException>(()=>system.SubmitBid(p,"bad",float.NaN,240,Player(),SampleProjectDataFactory.CreateCompetitors(),0));
            Assert.Throws<InvalidOperationException>(()=>system.SubmitBid(p,"late",42000,240,Player(),SampleProjectDataFactory.CreateCompetitors(),p.DeadlineHour));
            Assert.That(p.Status,Is.EqualTo(ProjectStatus.Available));Assert.That(rng.NextDouble(),Is.EqualTo(untouched.NextDouble()));
        }

        [Test]
        public void StarterFactories_ReturnIndependentVersionedData()
        {
            var first=SampleProjectDataFactory.CreateStarterProjects();var second=SampleProjectDataFactory.CreateStarterProjects();
            Assert.That(first.Count,Is.EqualTo(5));first[0].Budget=1;Assert.That(second[0].Budget,Is.Not.EqualTo(1));
            foreach(var p in second) { SaveMigrationRunner.Run(p,_=>{});Assert.That(p.SchemaVersion,Is.EqualTo(2));p.Validate(); }
            var profiles=SampleProjectDataFactory.CreateCompetitors();Assert.That(profiles.Count,Is.EqualTo(5));
            foreach(var profile in profiles) { profile.Validate();SaveMigrationRunner.Run(profile,_=>{});Assert.That(profile.SchemaVersion,Is.EqualTo(2)); }
        }

        [Test]
        public void LocalAndSpecialistStrength_RespondToMatchingProject()
        {
            var p=SampleProjectDataFactory.CreateStarterProjects()[2];var profiles=SampleProjectDataFactory.CreateCompetitors();var system=Create();
            double matched=system.CalculateWinProbability(p,p.Budget,p.EstimatedDurationHours,Player(),profiles);
            p.Region="Elsewhere";p.BuildingType="Other";
            Assert.That(system.CalculateWinProbability(p,p.Budget,p.EstimatedDurationHours,Player(),profiles),Is.GreaterThan(matched));
        }

        [Test]
        public void SubmittedListenerFailure_DoesNotStrandTender()
        {
            var p=SampleProjectDataFactory.CreateStarterProjects()[0];
            GameEvents.Subscribe<BidSubmittedEvent>(e=>throw new InvalidOperationException("listener failure"));
            Assert.Throws<InvalidOperationException>(()=>Create().SubmitBid(p,"bid",40000,240,Player(),new CompetitorCompanyData[0],0));
            Assert.That(p.Status,Is.EqualTo(ProjectStatus.Won));Assert.That(p.ContractValue,Is.EqualTo(40000));
        }
    }
}
