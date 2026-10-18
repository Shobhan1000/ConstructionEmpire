using System.Collections.Generic;
namespace ConstructionEmpire.Data.Bidding
{
    /// <summary>Provisional authored content. Editable assets are generated from these defaults.</summary>
    public static class SampleProjectDataFactory
    {
        public static List<ProjectData> CreateStarterProjects()
        {
            var titles=new[] { "House renovation", "Small warehouse", "Restaurant fit-out", "Small office renovation", "Residential extension" };
            var types=new[] { "Residential", "Warehouse", "Hospitality", "Office", "Residential" };
            var sizes=new[] { 85f,250f,120f,150f,45f };
            var durations=new[] { 240f,480f,320f,280f,400f };
            var budgets=new[] { 48000f,125000f,85000f,65000f,72000f };
            var labor=new[] { 600f,1400f,950f,800f,1100f };
            var materials=new[] { 120f,380f,230f,180f,220f };
            var equipment=new[] { 40f,150f,55f,45f,90f };
            var sub=new[] { 60f,100f,120f,70f,85f };
            var deadlines=new long[] { 720,1440,960,840,1200 };
            var penalties=new[] { 150f,400f,300f,200f,250f };
            var result=new List<ProjectData>();
            for (int i=0;i<titles.Length;i++)
            {
                var p=new ProjectData { Id="starter-"+(i+1),TemplateId="template-"+(i+1),Title=titles[i],BuildingType=types[i],
                    Region="Westmere",Location="Westmere district "+(i+1),ClientId="client-"+(i+1),ClientName=BiddingFlavorContent.Clients[i],
                    Size=sizes[i],EstimatedDurationHours=durations[i],Budget=budgets[i],DeadlineHour=deadlines[i],
                    QualityRequirement=i==2 ? QualityTier.Enhanced : QualityTier.Standard,PenaltyPerLateDay=penalties[i],
                    PaymentSchedule="To be agreed on contract award",Status=ProjectStatus.Available,
                    Requirements=new RequirementProfile { LaborHours=labor[i],MaterialUnits=materials[i],EquipmentHours=equipment[i],SubcontractorHours=sub[i] } };
                p.Validate();result.Add(p);
            }
            return result;
        }
        public static List<CompetitorCompanyData> CreateCompetitors()
        {
            var price=new[] { .82f,1.18f,1.05f,1.12f,.98f };
            var quality=new[] { .45f,.95f,.65f,.9f,.7f };
            var speed=new[] { 1.1f,1.1f,.7f,.95f,1f };
            var rep=new[] { 45f,90f,70f,85f,75f };
            var result=new List<CompetitorCompanyData>();
            for(int i=0;i<5;i++) result.Add(new CompetitorCompanyData {
                Id="competitor-"+(i+1),CompanyName=BiddingFlavorContent.Competitors[i],Personality=BiddingFlavorContent.Personalities[i],
                Archetype=(CompetitorArchetype)i,PriceMultiplier=price[i],Quality=quality[i],DurationMultiplier=speed[i],Reputation=rep[i],
                Experience=.6f,ClientRelationship=.4f,Certifications=.65f,MarketFit=.6f,HomeRegion="Westmere",Specialty="Hospitality",
                RegionalStrength=i==4 ? .8f : .1f,CategoryStrength=i==3 ? .8f : .1f });
            return result;
        }
        public static EstimationRates CreateRates() => new EstimationRates {
            LaborPerHour=30,MaterialPerUnit=90,EquipmentPerHour=45,SubcontractorPerHour=50,OverheadPerHour=4,OverheadFraction=.08f };
        public static BiddingConfiguration CreateBiddingConfiguration() => new BiddingConfiguration {
            Weights=new BidWeights { Price=3,Reputation=2,Experience=1,Quality=1,ClientRelationship=1,CompletionTime=1,Certifications=.5f,MarketConditions=.5f },
            ScoreSensitivity=6,NarrowLossProbabilityGap=.04f,CompetitivePriceThreshold=.9f,PremiumQualityThreshold=.85f,FastDurationThreshold=.85f };
    }
}
