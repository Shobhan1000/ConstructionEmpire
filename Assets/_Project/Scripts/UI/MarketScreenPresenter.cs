using System.Linq;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal static class MarketScreenPresenter
    {
        public static void Build(VisualElement content,MainDashboardController dashboard)
        {
            var market=dashboard.Session.Market.Current;DashboardElements.Label(content,"Market and competitors","card-title");
            DashboardElements.Label(content,$"{market.Season} · {market.State} · Demand {market.Demand:P0} · Labor availability {market.LaborAvailability:P0}","form-note");
            DashboardElements.Label(content,$"Materials {market.MaterialIndex:P0} · Wages {market.WageIndex:P0} · Equipment quotes {market.EquipmentIndex:P0} · New-loan rate adjustment {market.InterestAdjustment:+0.00%;-0.00%;0%}","form-note");DashboardElements.Label(content,market.News,"notification-description");
            foreach(var operation in dashboard.Session.CompetitorAI.Operations)
            {var c=dashboard.Session.Competitors.Single(x=>x.Id==operation.Id);DashboardElements.Label(content,$"{c.CompanyName} · {dashboard.Session.Bidding.GetQualitativeSignal(c)} · {operation.Wins} wins / {operation.Losses} losses · {operation.Employees} staff · {operation.Equipment} machines · {operation.Offices} offices · Reputation {c.Reputation:0}","table-row");}
        }
    }
}
