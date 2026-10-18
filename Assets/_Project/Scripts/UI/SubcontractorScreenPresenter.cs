using ConstructionEmpire.Data.Save;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Systems.Construction;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal static class SubcontractorScreenPresenter
    {
        public static void Build(VisualElement content,MainDashboardController dashboard,ProjectData project)
        {
            var session=dashboard.Session;var system=session.Subcontractors;var phases=session.Phases(project.Id);var graph=new PhaseDependencyGraph(phases);
            DashboardElements.Label(content,"Compare specialty quotes, then award eligible work. Your inventory and equipment support the awarded team.","form-note");
            foreach(var phase in phases.Where(x=>x.RequiresSubcontractor))
            {
                var card=DashboardElements.Box(content,"card");DashboardElements.Label(card,phase.Name+" · "+phase.SubcontractorSpecialty,"card-title");
                var award=system.Awards.FirstOrDefault(x=>x.Quote.ProjectId==project.Id && x.Quote.PhaseId==phase.Id);
                if(award!=null)
                {
                    var company=system.Companies.Single(x=>x.Id==award.Quote.CompanyId);
                    string state=award.Completed ? "Complete" : award.Delayed && session.Simulation.Time.TotalHours<=award.AvailableFromHour ? "Delayed · ready after hour "+award.AvailableFromHour : "Working · resources required";
                    var status=DashboardElements.Label(card,company.Name+" · "+state,"form-note");status.EnableInClassList("critical",state.StartsWith("Delayed"));
                    DashboardElements.Label(card,DashboardElements.Money((double)award.Quote.Price)+$" · {phase.Progress:0.0}% · phase risk {phase.Risk:P0}","form-note");continue;
                }
                var quotes=system.GetQuotes(project.Id,phase.Id);
                if(quotes.Count==0)DashboardElements.Button(card,"Request competing quotes",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.RequestQuotes,Id=project.Id,OtherId=phase.Id})),"primary");
                foreach(var quote in quotes)
                {
                    var company=system.Companies.Single(x=>x.Id==quote.CompanyId);var row=DashboardElements.Box(card,"table-row");row.style.flexWrap=Wrap.Wrap;
                    DashboardElements.Label(row,company.Name,"project-name-cell");DashboardElements.Label(row,DashboardElements.Money((double)quote.Price)+$" · {quote.DurationHours} h","cell");
                    DashboardElements.Label(row,quote.ReliabilitySignal+$" · Quality {quote.Quality:P0}","cell");row.tooltip=company.Personality;
                    var button=DashboardElements.Button(row,"Award work",()=>dashboard.AskConfirmation("Award "+phase.Name+" to "+company.Name+" for "+DashboardElements.Money((double)quote.Price)+"?",()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.AwardSubcontractor,Id=project.Id,OtherId=phase.Id,ThirdId=quote.Id})),"primary");
                    button.SetEnabled(project.Status==ProjectStatus.Active && graph.CanStart(phase.Id) && company.CanAcceptWork);
                }
                if(project.Status!=ProjectStatus.Active || !graph.CanStart(phase.Id))DashboardElements.Label(card,"Award after the project starts and prerequisite phases are complete.","form-note");
            }
            if(!phases.Any(x=>x.RequiresSubcontractor))DashboardElements.Label(content,"This template has no subcontracted phases.","empty-state");
            DashboardElements.Label(content,"Specialty companies","card-title");
            foreach(var company in system.Companies)DashboardElements.Label(content,company.Name+" · "+company.Specialty+" · "+(company.CanAcceptWork ? "Available" : "Unavailable")+" · "+company.Personality,"table-row");
        }
    }
}
