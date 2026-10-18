using System;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Expansion;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Expansion;
using ConstructionEmpire.Systems.Persistence;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class CareerAndSupplyTests
    {
        private static SessionStartData Origin(float cash=250000)
        {
            var projects=SampleProjectDataFactory.CreateStarterProjects();
            return new SessionStartData {Company=new CompanyData("Original",cash),Clock=new GameTimeData(),Seed=42,Projects=projects,Workforce=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment(),Phases=projects.ToDictionary(x=>x.Id,x=>SampleRenovationPhaseFactory.CreateHouseRenovationPhases()),Rates=SampleProjectDataFactory.CreateRates(),Bidding=SampleProjectDataFactory.CreateBiddingConfiguration(),Competitors=SampleProjectDataFactory.CreateCompetitors(),Management=ManagementSettings.CreateStarter(),Market=new MarketSettings {CompetitorBidChance=0,OpportunityBaseCount=0},Expansion=new ExpansionSettings {ProceduralOpportunities=true}};
        }
        private static void Dispose(ManagementSession s){s.Dispose();s.Simulation.Dispose();GameEvents.Clear();}
        private static void Replay(ManagementSession s){var save=s.Commands.Capture();using(var replay=new SessionReplay(save)){int guard=0;while(!replay.Step(17))if(++guard>1000)Assert.Fail("Replay timeout");Assert.That(SessionStateProof.Compute(replay.Session),Is.EqualTo(save.StateProof));}}
        [Test]public void EveryCityCanStartAnIndependentCareerWithLocalJobs()
        {
            var original=Origin();var before=SessionJson.Serialize(original);var cities=WorldAtlasFactory.Create();Assert.That(cities.Count,Is.EqualTo(12));Assert.That(cities.Select(r=>r.CountryId).Distinct().Count(),Is.EqualTo(4));
            foreach(var city in cities)
            {
                var origin=CareerSetup.Apply(original,new CareerOptions {RegionId=city.Id,CompanyName="Local Builder",Seed=81});
                Assert.That(origin.Regions.Count(r=>r.StartingRegion),Is.EqualTo(1));Assert.That(origin.Workforce.Workers[0].WagePerHour,Is.EqualTo(original.Workforce.Workers[0].WagePerHour*city.CostFactor).Within(.0001));
                var session=SessionFactory.Create(origin);try {Assert.That(session.Expansion.Offices[0].RegionId,Is.EqualTo(city.Id));Assert.That(session.Projects.All(p=>p.Region==city.Name && p.SiteMap.RegionId==city.Id),Is.True);Assert.That(session.Projects.All(p=>session.Expansion.DeliveryHours(p)<24),Is.True);Replay(session);}finally{Dispose(session);}
            }
            Assert.That(SessionJson.Serialize(original),Is.EqualTo(before));
        }
        [Test]public void OverseasOfficeGeneratesLocalOpportunitiesAndReplays()
        {
            var session=SessionFactory.Create(CareerSetup.Apply(Origin(2000000),new CareerOptions {RegionId="uk-london"}));
            try {
                var region=session.Expansion.Region("au-melbourne");double cash=session.Simulation.Company.CashBalance;double cost=session.Expansion.OfficeEntryCost(region);
                session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.OpenOffice,Id=region.Id});
                Assert.That(cash-session.Simulation.Company.CashBalance,Is.EqualTo(cost).Within(.1));
                var office=session.Expansion.Offices.Single(o=>o.RegionId==region.Id);session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.GenerateOpportunities,Id=office.Id});
                var local=session.Projects.Where(p=>p.SiteMap.RegionId==region.Id).ToList();Assert.That(local.Count,Is.GreaterThan(0));Assert.That(local.All(p=>session.Expansion.DeliveryHours(p)<24),Is.True);Replay(session);
            }finally{Dispose(session);}
        }
        [Test]public void PreGeneratorSaveFieldsCanBeAbsentWithoutChangingLegacyRules()
        {
            var origin=Origin();origin.Expansion.ProceduralOpportunities=false;origin.Expansion.OpportunityProfiles=null;
            var json=SessionJson.Serialize(origin).Replace("\"OpportunityProfiles\":null,","").Replace(",\"OpportunityProfiles\":null","").Replace("\"ProceduralOpportunities\":false,","").Replace(",\"ProceduralOpportunities\":false","");
            Assert.That(json,Does.Not.Contain("OpportunityProfiles"));Assert.That(json,Does.Not.Contain("ProceduralOpportunities"));
            var restored=SessionJson.Deserialize<SessionStartData>(json);Assert.That(restored.Expansion.ProceduralOpportunities,Is.False);restored.Expansion.Validate();
            var session=SessionFactory.Create(restored);try {Replay(session);}finally{Dispose(session);}
        }
        [Test]public void LegacyRegionsKeepNeutralCostsAndCopyWithoutSharingLists()
        {
            var region=SampleExpansionDataFactory.CreateStarterRegions()[0];Assert.That(region.CostFactor,Is.EqualTo(1));var copy=region.Copy();copy.ProjectTypes.Clear();Assert.That(region.ProjectTypes,Is.Not.Empty);
            var source=Origin();source.Regions=SampleExpansionDataFactory.CreateStarterRegions();var session=SessionFactory.Create(source);try{Assert.That(session.Expansion.Regions.Count,Is.EqualTo(4));Replay(session);}finally{Dispose(session);}
        }
        [Test]public void SupplyPreviewIsReadOnlyAndPurchaseDoesNotDuplicateIncomingStock()
        {
            var session=SessionFactory.Create(Origin());try {
                string id=session.Projects[0].Id;var before=SessionStateProof.Compute(session);var plan=SupplyPlanner.Preview(session,id,SupplyPreference.Reliability);Assert.That(plan.Problems,Is.Empty);Assert.That(plan.Lines,Is.Not.Empty);Assert.That(SessionStateProof.Compute(session),Is.EqualTo(before));
                session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.OrderSupplyPlan,Id=id,Start=(int)SupplyPreference.Reliability,Amount=plan.Total});
                Assert.That(session.Orders.Count,Is.EqualTo(plan.Lines.Count));Assert.That(SupplyPlanner.Preview(session,id,SupplyPreference.Reliability).Lines,Is.Empty);Replay(session);
            }finally{Dispose(session);}
        }
        [Test]public void InsufficientCashOrUnapprovedPricePlacesNoOrders()
        {
            foreach(float cash in new[]{1f,250000f}){var session=SessionFactory.Create(Origin(cash));try{var plan=SupplyPlanner.Preview(session,session.Projects[0].Id,SupplyPreference.Reliability);var before=SessionStateProof.Compute(session);Assert.Throws<InvalidOperationException>(()=>SupplyPlanner.Purchase(session,session.Projects[0].Id,SupplyPreference.Reliability,cash==1?plan.Total:0));Assert.That(session.Orders,Is.Empty);Assert.That(SessionStateProof.Compute(session),Is.EqualTo(before));}finally{Dispose(session);}}
        }
        [Test]public void SupplierPoliciesHavePredictableTradeoffs()
        {
            var session=SessionFactory.Create(Origin());try{string id=session.Projects[0].Id;var reliable=SupplyPlanner.Preview(session,id,SupplyPreference.Reliability);var cheap=SupplyPlanner.Preview(session,id,SupplyPreference.LowestCost);var fast=SupplyPlanner.Preview(session,id,SupplyPreference.Fastest);Assert.That(cheap.Total,Is.LessThanOrEqualTo(reliable.Total));foreach(var line in fast.Lines)Assert.That(line.LeadHours,Is.LessThanOrEqualTo(reliable.Lines.Single(x=>x.MaterialId==line.MaterialId).LeadHours));}finally{Dispose(session);}
        }
    }
}
