using System;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Persistence
{
    public static class SessionFactory
    {
        public static ManagementSession Create(SessionStartData source)
        {
            if(source==null || source.Clock==null || source.Company==null || source.Clock.PendingSimulationSeconds!=0)throw new ArgumentException("A clean starting company and clock are required.");
            // Separate the retained origin from runtime objects modified by systems.
            var d=SessionJson.Copy(source);var sim=new SimulationManager(d.Company,d.Clock,d.Seed,d.CrewEfficiency);
            try
            {
                var session=new ManagementSession(sim,d.Projects,d.Workforce,d.Phases,d.Rates,d.Bidding,d.Competitors,d.Management,d.Finance,d.Suppliers,d.Procurement,d.Subcontractors,d.Subcontracting,d.Risks,d.Regions,d.Expansion,d.Market,d.Research,d.ResearchSettings,d.Development);
                session.Commands.Initialize(source);return session;
            }
            catch{sim.Dispose();throw;}
        }
    }
}
