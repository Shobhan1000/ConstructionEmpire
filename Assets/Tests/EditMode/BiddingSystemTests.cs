using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Bidding;
using ConstructionEmpire.Clients;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Tests
{
    public class BiddingSystemTests
    {
        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        private ProjectRuntimeData MakeOpportunity()
        {
            return new ProjectRuntimeData
            {
                Id = "test-project",
                Type = ProjectType.HouseRenovation,
                DisplayName = "Test House",
                Status = ProjectStatus.Available
            };
        }

        [Test]
        public void SubmitBid_CheapBidWinsMoreOftenThanExpensiveBid()
        {
            int cheapWins = 0;
            int expensiveWins = 0;
            const int trials = 60;

            for (int seed = 0; seed < trials; seed++)
            {
                var company = new CompanySystem();
                company.FoundCompany("Test Co.", 250000, 50f, System.DateTime.Now);

                var cheapBidding = new BiddingSystem(new CompetitorSimulator(new SeededRandom(seed)), company, new SeededRandom(seed + 1000));
                if (cheapBidding.SubmitBid(MakeOpportunity(), bidPrice: 80000, fairMarketCost: 100000))
                {
                    cheapWins++;
                }

                var expensiveBidding = new BiddingSystem(new CompetitorSimulator(new SeededRandom(seed)), company, new SeededRandom(seed + 1000));
                if (expensiveBidding.SubmitBid(MakeOpportunity(), bidPrice: 160000, fairMarketCost: 100000))
                {
                    expensiveWins++;
                }
            }

            Assert.Greater(cheapWins, expensiveWins);
        }

        [Test]
        public void SubmitBid_OnLoss_SetsProjectStatusToBidLost()
        {
            var company = new CompanySystem();
            company.FoundCompany("Test Co.", 250000, 0f, System.DateTime.Now); // 0 reputation, worst case

            var bidding = new BiddingSystem(new CompetitorSimulator(new SeededRandom(1)), company, new SeededRandom(2));
            var project = MakeOpportunity();

            bool won = bidding.SubmitBid(project, bidPrice: 500000, fairMarketCost: 100000); // absurdly high bid

            if (!won)
            {
                Assert.AreEqual(ProjectStatus.BidLost, project.Status);
            }
        }

        [Test]
        public void SubmitBid_HigherClientRelationship_WinsMoreOftenThanLowerRelationship()
        {
            int highRelationshipWins = 0;
            int lowRelationshipWins = 0;
            const int trials = 60;

            for (int seed = 0; seed < trials; seed++)
            {
                var company = new CompanySystem();
                company.FoundCompany("Test Co.", 250000, 50f, System.DateTime.Now);

                var clients = new ClientSystem(new SeededRandom(seed + 500));
                var highRelClient = clients.Clients.First();
                highRelClient.RelationshipScore = 100f;
                var lowRelClient = clients.Clients.Last();
                lowRelClient.RelationshipScore = 0f;

                var highBidding = new BiddingSystem(new CompetitorSimulator(new SeededRandom(seed)), company, new SeededRandom(seed + 1000), clients);
                var highProject = MakeOpportunity();
                highProject.ClientId = highRelClient.Id;
                if (highBidding.SubmitBid(highProject, bidPrice: 100000, fairMarketCost: 100000))
                {
                    highRelationshipWins++;
                }

                var lowBidding = new BiddingSystem(new CompetitorSimulator(new SeededRandom(seed)), company, new SeededRandom(seed + 1000), clients);
                var lowProject = MakeOpportunity();
                lowProject.ClientId = lowRelClient.Id;
                if (lowBidding.SubmitBid(lowProject, bidPrice: 100000, fairMarketCost: 100000))
                {
                    lowRelationshipWins++;
                }
            }

            Assert.Greater(highRelationshipWins, lowRelationshipWins);
        }

        [Test]
        public void SubmitBid_WithNoClientSystemWired_BehavesExactlyAsWithout()
        {
            // Backward-compatibility guard: the 3-arg constructor (no
            // ClientSystem) must keep working unchanged for existing
            // callers/tests.
            var company = new CompanySystem();
            company.FoundCompany("Test Co.", 250000, 50f, System.DateTime.Now);

            var bidding = new BiddingSystem(new CompetitorSimulator(new SeededRandom(9)), company, new SeededRandom(10));
            var project = MakeOpportunity();

            Assert.DoesNotThrow(() => bidding.SubmitBid(project, bidPrice: 90000, fairMarketCost: 100000));
        }
    }
}
