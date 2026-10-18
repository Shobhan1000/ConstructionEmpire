using System;
using System.Linq;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Expansion;
using ConstructionEmpire.Systems.Persistence;
namespace ConstructionEmpire.Systems.Expansion
{
    public static class CareerSetup
    {
        public static SessionStartData Apply(SessionStartData source,CareerOptions options)
        {
            if(source==null || options==null)throw new ArgumentNullException();
            var regions=WorldAtlasFactory.Create();var home=regions.SingleOrDefault(x=>x.Id==options.RegionId)??throw new ArgumentException("Choose a valid starting city.");
            float cash=options.StartingCash==-1?source.Company.CashBalance:options.StartingCash;
            if(float.IsNaN(cash) || float.IsInfinity(cash) || cash<=0)throw new ArgumentException("Starting capital must be positive.");
            string name=options.CompanyName==null?source.Company.CompanyName:options.CompanyName.Trim();if(name.Length==0 || name.Length>60)throw new ArgumentException("Company name must contain 1–60 characters.");
            var origin=SessionJson.Copy(source);origin.Company=new CompanyData(name,cash);origin.Seed=options.Seed;origin.Regions=regions;home.StartingRegion=true;
            origin.Management.OfficeName=home.Name+" Headquarters";origin.Management.OfficeLocation=home.Name+", "+home.CountryName;
            foreach(var project in origin.Projects){project.Region=home.Name;project.Location=home.Name+" district "+(origin.Projects.IndexOf(project)+1);project.Budget*=(float)home.PriceMultiplier;}
            foreach(var worker in origin.Workforce.Workers)worker.WagePerHour*=home.CostFactor;
            foreach(var competitor in origin.Competitors)competitor.HomeRegion=home.Name;
            origin.Expansion=origin.Expansion??new ExpansionSettings();origin.Expansion.ProceduralOpportunities=true;
            if(origin.Expansion.OpportunityProfiles==null || origin.Expansion.OpportunityProfiles.Count==0)origin.Expansion.OpportunityProfiles=OpportunityProfileCatalog.Create();
            return origin;
        }
    }
}
