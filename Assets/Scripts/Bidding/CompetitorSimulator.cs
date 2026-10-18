using System;
using System.Collections.Generic;
using ConstructionEmpire.Data;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Bidding
{
    public class CompetitorBid
    {
        public string CompetitorName;
        public CompetitorStrategy Strategy;
        public double BidAmount;
    }

    /// <summary>
    /// Lightweight background competitor simulation (spec sections 4 and
    /// 46). Not a full competing-company simulation - just enough price
    /// variance to make the player's bid price a real decision instead of
    /// a guaranteed win.
    /// </summary>
    public class CompetitorSimulator
    {
        private readonly SeededRandom _random;

        private static readonly (string Name, CompetitorStrategy Strategy, double PriceMultiplierMin, double PriceMultiplierMax)[] Roster =
        {
            ("Ironclad Builders", CompetitorStrategy.LowCost, 0.85, 0.98),
            ("Meridian Construction Group", CompetitorStrategy.Premium, 1.05, 1.25),
            ("Rapid Frame Co.", CompetitorStrategy.Fast, 0.95, 1.10),
            ("Coastal Specialty Contractors", CompetitorStrategy.Specialist, 0.90, 1.15),
            ("Hometown Builders", CompetitorStrategy.Local, 0.92, 1.05)
        };

        public CompetitorSimulator(SeededRandom random)
        {
            _random = random;
        }

        public List<CompetitorBid> SimulateCompetitorBids(double fairMarketCost, int competitorCount)
        {
            var bids = new List<CompetitorBid>();
            competitorCount = Math.Min(competitorCount, Roster.Length);

            for (int i = 0; i < competitorCount; i++)
            {
                var profile = Roster[i];
                double multiplier = _random.NextFloat((float)profile.PriceMultiplierMin, (float)profile.PriceMultiplierMax);
                bids.Add(new CompetitorBid
                {
                    CompetitorName = profile.Name,
                    Strategy = profile.Strategy,
                    BidAmount = fairMarketCost * multiplier
                });
            }

            return bids;
        }
    }
}
