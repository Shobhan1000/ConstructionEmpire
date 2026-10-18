using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Clients;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Bidding
{
    public readonly struct BidWonEvent : IGameEvent
    {
        public readonly string ProjectId;
        public BidWonEvent(string projectId) { ProjectId = projectId; }
    }

    public readonly struct BidLostEvent : IGameEvent
    {
        public readonly string ProjectId;
        public readonly string WinningCompetitor;
        public BidLostEvent(string projectId, string winningCompetitor)
        {
            ProjectId = projectId;
            WinningCompetitor = winningCompetitor;
        }
    }

    /// <summary>
    /// Submits a player bid against simulated competitors and resolves
    /// win/loss (spec sections 3-4). Does not move a won project into
    /// active construction itself - ConstructionProjectSystem listens for
    /// BidWonEvent (or is called directly by whoever orchestrates the
    /// flow) and takes it from there, keeping the two systems decoupled.
    /// </summary>
    public class BiddingSystem
    {
        private readonly CompetitorSimulator _competitorSimulator;
        private readonly CompanySystem _companySystem;
        private readonly SeededRandom _random;
        private readonly ClientSystem _clientSystem;

        public BiddingSystem(CompetitorSimulator competitorSimulator, CompanySystem companySystem, SeededRandom random, ClientSystem clientSystem = null)
        {
            _competitorSimulator = competitorSimulator;
            _companySystem = companySystem;
            _random = random;
            _clientSystem = clientSystem;
        }

        /// <summary>
        /// Submits bidPrice for the given project. fairMarketCost should be
        /// the project's own estimated cost before profit (e.g.
        /// BidEstimateData.Subtotal) - used only to generate plausible
        /// competitor bids around the same job, never shown to the player
        /// directly (spec section 4: player shouldn't know exact
        /// competitor bids).
        /// </summary>
        public bool SubmitBid(ProjectRuntimeData project, double bidPrice, double fairMarketCost, int competitorCount = 3)
        {
            var competitorBids = _competitorSimulator.SimulateCompetitorBids(fairMarketCost, competitorCount);

            double reputation = _companySystem.Data != null ? _companySystem.Data.Reputation : 50f;
            var client = _clientSystem?.GetClient(project.ClientId);
            double winProbability = ComputeWinProbability(bidPrice, competitorBids, reputation, client);

            bool won = _random.NextDouble() < winProbability;

            if (won)
            {
                project.ContractValue = bidPrice;
                EventBus.Publish(new BidWonEvent(project.Id));
            }
            else
            {
                project.Status = ProjectStatus.BidLost;
                string winner = competitorBids.Count > 0
                    ? competitorBids.OrderBy(b => b.BidAmount).First().CompetitorName
                    : "a competitor";
                EventBus.Publish(new BidLostEvent(project.Id, winner));
            }

            return won;
        }

        /// <summary>
        /// Rough win-probability model: cheaper-than-average bids and
        /// higher reputation both help, with a small nudge from client
        /// relationship on top (repeat clients are a little more likely to
        /// pick you again - spec section 31). Clamped to [0.05, 0.95] so
        /// no combination of inputs ever makes the outcome fully certain.
        /// </summary>
        private double ComputeWinProbability(double bidPrice, List<CompetitorBid> competitorBids, double reputation, ClientData client)
        {
            if (competitorBids.Count == 0) return 0.9;

            double averageCompetitorBid = competitorBids.Average(b => b.BidAmount);
            if (averageCompetitorBid <= 0) return 0.5;

            double priceRatio = bidPrice / averageCompetitorBid;  // <1 = cheaper than average
            double priceFactor = Clamp01(1.4 - priceRatio);       // ~0.9 at 50% of average, ~0.4 at market average, ~0 at 140%+

            double reputationFactor = reputation / 100.0;         // 0-1

            double probability = (priceFactor * 0.75) + (reputationFactor * 0.25);

            if (client != null)
            {
                double relationshipBonus = (client.RelationshipScore - 50.0) / 100.0 * 0.15; // -0.075 to +0.075
                probability += relationshipBonus;
            }

            return Clamp01(probability, 0.05, 0.95);
        }

        private static double Clamp01(double value) => Clamp01(value, 0.0, 1.0);

        private static double Clamp01(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
