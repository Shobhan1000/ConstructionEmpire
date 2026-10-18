using System.Collections.Generic;
namespace ConstructionEmpire.Data.Subcontractors
{
    public static class SampleSubcontractorDataFactory
    {
        public static List<SubcontractorCompanyData> CreateStarterSubcontractors()
        {
            string[] names={"Brightline Electrical","Budget Circuit Services","Clearflow Plumbing","ValuePipe Contractors","Northwind Mechanical","BreezeRate HVAC","Crownridge Roofing","Economy Eaves","Greenhaven Landscapes","Patch & Plot Works","Ironstone Structures","LeanSpan Structural"};
            string[] personality={"Premium electrical crews with dependable schedules.","Low electrical quotes with occasional scheduling slips.","Reliable plumbing specialists at steady mid-market prices.","Budget plumbing crews suited to flexible timelines.","Premium HVAC commissioning with consistent delivery.","Competitive HVAC quotes with variable lead times.","Dependable roofing crews with a quality-first price.","Low roofing prices with occasional dispatch delays.","Reliable landscape finishing at a modest premium.","Affordable landscaping with a flexible diary.","Premium structural specialists with strong quality control.","Lean structural pricing with a higher schedule risk."};
            var result=new List<SubcontractorCompanyData>();
            for(int i=0;i<12;i++)result.Add(new SubcontractorCompanyData {Id="subcontractor-"+(i+1),Name=names[i],Personality=personality[i],Specialty=(SubcontractorSpecialty)(i/2),QuotePricingTendency=i%2==0 ? 1.15 : .8,Reliability=i%2==0 ? .97 : .65,Quality=i%2==0 ? .95 : .75,Reputation=i%2==0 ? 85 : 60,TimelineMultiplier=i%2==0 ? .9 : 1.1,Capacity=1});
            return result;
        }
    }
}
