using System;
using System.Collections.Generic;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Randomness;
namespace ConstructionEmpire.Systems.Bidding
{
    public readonly struct BidFactors
    {
        public double Price { get; }
        public double Reputation { get; }
        public double Experience { get; }
        public double Quality { get; }
        public double ClientRelationship { get; }
        public double CompletionTime { get; }
        public double Certifications { get; }
        public double MarketConditions { get; }
        public BidFactors(double price, double reputation, double experience, double quality, double relationship, double time, double certifications, double market)
        { Price=price; Reputation=reputation; Experience=experience; Quality=quality; ClientRelationship=relationship; CompletionTime=time; Certifications=certifications; MarketConditions=market; }
        public double WeightedScore(BidWeights w) => (Price*w.Price + Reputation*w.Reputation + Experience*w.Experience + Quality*w.Quality +
            ClientRelationship*w.ClientRelationship + CompletionTime*w.CompletionTime + Certifications*w.Certifications + MarketConditions*w.MarketConditions)/w.Total;
    }
    public readonly struct BidOutcome
    {
        public bool Won { get; }
        public bool NarrowlyLost { get; }
        public BidOutcome(bool won, bool narrowlyLost) { Won=won; NarrowlyLost=narrowlyLost; }
    }
    public sealed class BiddingSystem
    {
        private readonly BiddingConfiguration config;
        private readonly SeededRandom random;
        private bool resolving;
        public double MarketDemand {get;set;}=1;public double MarketPricePressure {get;set;}=.15;
        public BiddingSystem(BiddingConfiguration config, SeededRandom random)
        { this.config=config ?? throw new ArgumentNullException(nameof(config)); this.random=random ?? throw new ArgumentNullException(nameof(random)); config.Validate(); }

        public BidFactors CalculatePlayerFactors(ProjectData project, float price, float durationHours, BidderCapabilities player)
        {
            if (project==null || player==null) throw new ArgumentNullException();
            project.Validate(); player.Validate(); config.Validate();
            BidValidation.Positive(price, nameof(price)); BidValidation.Positive(durationHours, nameof(durationHours));
            // Bounded ratios avoid score overflow while preserving monotonic price/time response.
            return new BidFactors(project.Budget/((double)project.Budget+price), player.Reputation/100d,
                player.Experience, player.Quality, player.ClientRelationship,
                project.EstimatedDurationHours/((double)project.EstimatedDurationHours+durationHours), player.Certifications, player.MarketFit);
        }

        public double CalculateWinProbability(ProjectData project, float price, float durationHours, BidderCapabilities player,
            IReadOnlyList<CompetitorCompanyData> competitors)
        {
            var factors = CalculatePlayerFactors(project, price, durationHours, player);
            if (competitors==null) throw new ArgumentNullException(nameof(competitors));
            var scores = new List<double> { factors.WeightedScore(config.Weights) };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var competitor in competitors)
            {
                if (competitor==null) throw new ArgumentException("Null competitor.");
                competitor.Validate(); if (!ids.Add(competitor.Id)) throw new ArgumentException("Duplicate competitor ID.");
                double experience = competitor.Experience;
                if (string.Equals(competitor.Specialty, project.BuildingType, StringComparison.OrdinalIgnoreCase))
                    experience += (1-experience)*competitor.CategoryStrength;
                double relationship = competitor.ClientRelationship;
                if (string.Equals(competitor.HomeRegion, project.Region, StringComparison.OrdinalIgnoreCase))
                    relationship += (1-relationship)*competitor.RegionalStrength;
                // Competitor price tendency is internal only; no exact competitor quote leaves this system.
                var other = new BidFactors(1d/(1d+competitor.PriceMultiplier*Math.Max(.5,1+(MarketDemand-1)*MarketPricePressure)), competitor.Reputation/100d,
                    experience, competitor.Quality, relationship, 1d/(1d+competitor.DurationMultiplier),
                    competitor.Certifications, competitor.MarketFit);
                scores.Add(other.WeightedScore(config.Weights));
            }
            double max = scores[0]; foreach (double score in scores) max=Math.Max(max,score);
            double total=0; foreach (double score in scores) total+=Math.Exp((score-max)*config.ScoreSensitivity);
            return Math.Exp((scores[0]-max)*config.ScoreSensitivity)/total;
        }

        public string GetQualitativeSignal(CompetitorCompanyData competitor)
        {
            if (competitor==null) throw new ArgumentNullException(nameof(competitor));
            competitor.Validate(); config.Validate();
            var signals = new List<string>();
            if (competitor.PriceMultiplier <= config.CompetitivePriceThreshold) signals.Add("Price-focused");
            if (competitor.Quality >= config.PremiumQualityThreshold) signals.Add("Quality-led");
            if (competitor.DurationMultiplier <= config.FastDurationThreshold) signals.Add("Fast delivery");
            if (competitor.Archetype==CompetitorArchetype.Specialist) signals.Add("Specialist expertise");
            if (competitor.Archetype==CompetitorArchetype.Local) signals.Add("Strong local relationships");
            return signals.Count==0 ? "Balanced contender" : string.Join("; ",signals);
        }

        /// <summary>Immediate tender resolution. One seeded draw per valid submission; duplicate bids are rejected.</summary>
        public BidOutcome SubmitBid(ProjectData project, string bidId, float price, float durationHours, BidderCapabilities player,
            IReadOnlyList<CompetitorCompanyData> competitors, long currentHour)
        {
            if (resolving) throw new InvalidOperationException("Bid resolution cannot be re-entered.");
            if (project==null) throw new ArgumentNullException(nameof(project));
            BidValidation.Text(bidId, nameof(bidId));
            if (project.Status!=ProjectStatus.Available) throw new InvalidOperationException("Project is not available for bidding.");
            if (currentHour<0 || currentHour>project.DeadlineHour || durationHours>project.DeadlineHour-currentHour)
                throw new InvalidOperationException("Proposed completion exceeds the project deadline.");
            double probability=CalculateWinProbability(project,price,durationHours,player,competitors);
            // Compute the final result before publishing. An observer failure must not strand a bid in Bidding.
            double draw=random.NextDouble(); bool won=draw<probability;
            bool narrow=!won && draw-probability<=config.NarrowLossProbabilityGap;
            // Use the same resolved draw to choose a named rival without exposing private offers.
            CompetitorCompanyData selected=null;
            if(!won && competitors.Count>0){double sum=0;var strengths=new List<double>();foreach(var c in competitors){double strength=(1/c.PriceMultiplier+c.Quality+c.Reputation/100+1/c.DurationMultiplier);strengths.Add(strength);sum+=strength;}double target=(draw-probability)/Math.Max(1e-12,1-probability)*sum;for(int i=0;i<competitors.Count;i++){target-=strengths[i];if(target<=0){selected=competitors[i];break;}}selected=selected??competitors[competitors.Count-1];}
            project.AwardedCompetitorId=selected?.Id;project.AwardedCompetitorName=selected?.CompanyName;
            resolving=true;
            project.SubmittedBidId=bidId; project.SubmittedBidAmount=price; project.Status=ProjectStatus.Bidding;
            try
            {
                try { GameEvents.Publish(new BidSubmittedEvent(bidId,project.Id,price)); }
                finally
                {
                    project.Status=won ? ProjectStatus.Won : ProjectStatus.Lost;
                    project.ContractValue=won ? price : 0;
                    if (won) GameEvents.Publish(new BidWonEvent(bidId,project.Id));
                    else {GameEvents.Publish(new BidLostEvent(bidId,project.Id,(narrow?"Narrowly lost to ":"Lost to ")+(selected?.CompanyName??"another contractor")));if(selected!=null)GameEvents.Publish(new ConstructionEmpire.Data.Market.CompetitorClaimedContractEvent(project.Id,selected.Id,selected.CompanyName,true));}
                }
                return new BidOutcome(won,narrow);
            }
            finally { resolving=false; }
        }
    }
}
