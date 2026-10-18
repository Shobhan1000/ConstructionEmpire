using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Scheduling;
namespace ConstructionEmpire.Systems.Management
{
    public sealed class ProjectNextAction {public string Title,Description,Screen,ProjectId;public bool Warning;}
    public static class ProjectReadiness
    {
        public static ProjectNextAction Get(ManagementSession session,ProjectData project)
        {
            ProjectNextAction Action(string title,string description,string screen,bool warning=false)=>new ProjectNextAction {Title=title,Description=description,Screen=screen,ProjectId=project.Id,Warning=warning};
            if(project.Status==ProjectStatus.Available)return Action("Review this opportunity","Compare the full estimate, deadline and client budget before bidding.","Bidding");
            if(project.Status==ProjectStatus.Won)return Action("Ready to mobilise","Review the contract and start work from Project Detail.","Project Detail");
            if(project.Status==ProjectStatus.Complete)return Action("Review the result","Check profit, final receipts and warranty retention.","Finance");
            if(project.Status==ProjectStatus.Lost)return Action("Find the next opportunity","Filter available projects and compare a different brief.","Projects");
            var phases=session.Phases(project.Id);var eligible=phases.Where(p=>!p.IsComplete && p.PrerequisiteIds.All(id=>phases.Single(x=>x.Id==id).IsComplete)).ToList();
            var blocked=session.Scheduling.Bookings.FirstOrDefault(b=>b.Open && b.Data.ProjectId==project.Id && b.Data.Status==BookingStatus.Blocked);
            if(blocked!=null)return Action("Reservation blocked",blocked.Data.PhaseId+": "+blocked.Data.Conflict,"Schedule",true);
            var empty=eligible.SelectMany(p=>p.MaterialRequirements).FirstOrDefault(m=>m.Quantity>0 && session.Inventory.GetQuantity(m.MaterialId)<=0);
            if(empty!=null)return Action("Supplies needed",empty.MaterialId+" is out of stock. Review outstanding deliveries or prepare a supply plan.","Materials",true);
            var broken=session.Workforce.Equipment.FirstOrDefault(e=>e.CurrentProjectId==project.Id && (e.Status==EquipmentStatus.Broken || e.Condition<=0 || e.EffectiveFuelPerHour>0 && e.CurrentFuel<=0));
            if(broken!=null)return Action("Equipment needs attention",broken.Name+" needs repair or fuel before it can work.","Equipment",true);
            var unassigned=eligible.FirstOrDefault(p=>!(p.RequiresSubcontractor && session.Subcontractors.Awards.Any(a=>a.Quote.ProjectId==project.Id && a.Quote.PhaseId==p.Id && !a.Completed)) && !session.Scheduling.Bookings.Any(b=>b.Open && b.Data.ProjectId==project.Id && b.Data.PhaseId==p.Id));
            if(unassigned!=null)return Action(unassigned.RequiresSubcontractor?"Specialist work ready":"Crew assignment needed",unassigned.Name+" has completed its prerequisites.",unassigned.RequiresSubcontractor?"Subcontractors":"Schedule",true);
            return Action("Monitor scheduled work","Check shifts, travel, subcontractor arrival, inspections and weather if progress is slow.","Schedule");
        }
    }
}
