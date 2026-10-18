using System;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Data.Market
{
    public enum MarketState { Recession,Slow,Normal,Growth,Boom }
    public enum Season { Spring,Summer,Autumn,Winter }
    [Serializable] public sealed class MarketSettings
    {
        public int UpdateEveryHours=168,CompetitorEveryHours=72,TenderGraceHours=168,MonthsPerSeason=3,OpportunityBaseCount=2;
        public double MinimumDemand=.35,MaximumDemand=1.8,DemandStep=.15,MeanReversion=.08;
        public double MaterialSensitivity=.4,WageSensitivity=.25,EquipmentSensitivity=.3,InterestSensitivity=.025;
        public double MinimumPriceFactor=.65,MaximumPriceFactor=1.65,ShortageChance=.1,MaterialShortagePremium=.18,LaborShortagePremium=.12;
        public double CompetitorBidChance=.75,CompetitorDevelopmentChance=.3,CompetitorFailureChance=.08,PriceVariation=.08,PricePressure=.15;
        public int InitialEmployees=8,InitialEquipment=2,MaximumScale=5,WinsForGrowth=2,LossesForShrink=3;
        public double ReputationPerWin=1,ReputationPerLoss=.2;
        public double[] SeasonalDemand={1.1,1.2,1,.7},SeasonalWeatherFrequency={1, .65,1.2,1.6};
        public void Validate()
        {
            if(UpdateEveryHours<1 || CompetitorEveryHours<1 || TenderGraceHours<1 || MonthsPerSeason<1 || OpportunityBaseCount<0 || InitialEmployees<1 || InitialEquipment<1 || MaximumScale<1 || WinsForGrowth<1 || LossesForShrink<1)throw new ArgumentException("Invalid market cadence/capacity.");
            ConstructionValidation.Positive(MinimumDemand,"Minimum demand");ConstructionValidation.Positive(MaximumDemand,"Maximum demand");if(MinimumDemand>1 || MaximumDemand<1)throw new ArgumentException("Demand bounds must contain neutral demand.");
            ConstructionValidation.Positive(MinimumPriceFactor,"Minimum price");ConstructionValidation.Positive(MaximumPriceFactor,"Maximum price");if(MinimumPriceFactor>1 || MaximumPriceFactor<1)throw new ArgumentException("Price bounds must contain one.");
            foreach(var x in new[]{DemandStep,MaterialSensitivity,WageSensitivity,EquipmentSensitivity,InterestSensitivity,MaterialShortagePremium,LaborShortagePremium,ReputationPerWin,ReputationPerLoss})ConstructionValidation.NonNegative(x,"Market setting");
            foreach(var x in new[]{MeanReversion,ShortageChance,CompetitorBidChance,CompetitorDevelopmentChance,CompetitorFailureChance,PriceVariation,PricePressure})ConstructionValidation.Unit(x,"Probability/fraction");
            if(SeasonalDemand==null || SeasonalWeatherFrequency==null || SeasonalDemand.Length!=4 || SeasonalWeatherFrequency.Length!=4)throw new ArgumentException("Four seasons required.");foreach(var x in SeasonalDemand)ConstructionValidation.Positive(x,"Seasonal demand");foreach(var x in SeasonalWeatherFrequency)ConstructionValidation.NonNegative(x,"Seasonal weather");
        }
    }
    public readonly struct MarketChangedEvent { public readonly MarketSnapshot Snapshot;public MarketChangedEvent(MarketSnapshot state){Snapshot=state;} }
    public readonly struct CompetitorClaimedContractEvent
    {public readonly string ProjectId,CompetitorId,CompetitorName;public readonly bool PlayerSubmitted;public CompetitorClaimedContractEvent(string project,string id,string name,bool playerSubmitted){ProjectId=project;CompetitorId=id;CompetitorName=name;PlayerSubmitted=playerSubmitted;}}
    public readonly struct CompetitorDevelopmentEvent
    {public readonly string CompetitorId,Description;public CompetitorDevelopmentEvent(string id,string description){CompetitorId=id;Description=description;}}
    public sealed class MarketSnapshot
    {
        public long Hour {get;}public MarketState State {get;}public Season Season {get;}public double Demand {get;}public double MaterialIndex {get;}public double WageIndex {get;}public double EquipmentIndex {get;}public double LaborAvailability {get;}public double InterestAdjustment {get;}public string News {get;}
        public MarketSnapshot(long hour,MarketState state,Season season,double demand,double material,double wage,double equipment,double labor,double interest,string news){Hour=hour;State=state;Season=season;Demand=demand;MaterialIndex=material;WageIndex=wage;EquipmentIndex=equipment;LaborAvailability=labor;InterestAdjustment=interest;News=news;}
    }
    public static class MarketNewsContent
    {
        public const string Boom="Construction boom: full order books are lifting demand across the region.";
        public const string Recession="Investment slows as clients defer projects and contractors compete for fewer tenders.";
        public const string Materials="Material shortages tighten supply, putting fresh pressure on project budgets.";
        public const string Labor="Skilled labor is in short supply as contractors compete to secure experienced crews.";
        public const string Winter="Seasonal slowdown: winter conditions cool demand for outdoor construction.";
        public const string Infrastructure="Infrastructure spending rises, bringing a new wave of regional construction opportunities.";
        public static string[] All=>new[]{Boom,Recession,Materials,Labor,Winter,Infrastructure};
    }
}
