using ConstructionEmpire.Bootstrap;
using System;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Presentation;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal sealed class TutorialPresenter : IDisposable
    {
        private readonly MainDashboardController dashboard;
        private readonly EventSubscriptions subscriptions=new EventSubscriptions();
        private readonly TutorialProgressData state=new TutorialProgressData();
        private readonly VisualElement view,body;private readonly Label title,text;private readonly Button toggle,action;
        private bool dismissed;
        private static readonly string[] Titles=new[]{"Find your first opportunity","Build an estimate","Submit your offer","Read the decision","Start the contract","Prepare the first phase","Complete and review the phase"};
        private static readonly string[] Texts=new[]{"Open Bidding and compare the available projects, their budget, deadline and quality requirement.","Review labor, materials, equipment, subcontractors and overhead; adjust profit and contingency, then choose Review estimate.","Review your price and submit the bid; a lower price may help you win but leaves less room for mistakes.","A win creates a contract; if you lose, compare another opportunity and try again without assuming the cheapest bid is best.","Open the won project, review its payment schedule in Finance, then start the project.","In Schedule, assign a crew to an eligible phase; check equipment and Materials, order shortages, and run at 1× until work begins.","Watch the phase reach 100%; if it stalls, check material stock, equipment, assignments and alerts before spending more."};
        public TutorialPresenter(MainDashboardController owner,VisualElement host)
        {
            dashboard=owner;view=DashboardElements.Box(host,"tutorial-card","tutorial");
            var heading=DashboardElements.Box(view,"card-heading");title=DashboardElements.Label(heading,"","card-title");
            toggle=DashboardElements.Button(heading,"Hide guide",()=>{dismissed=!dismissed;Render();});
            body=DashboardElements.Box(view,"tutorial-body");text=DashboardElements.Label(body,"","form-note");
            var actions=DashboardElements.Box(body,"card-heading");action=DashboardElements.Button(actions,"Open relevant screen",Go);
            DashboardElements.Button(actions,"Restart guide",()=>{state.Reset();Render();});
            subscriptions.Subscribe<BidSubmittedEvent>(e=>{if(Own(e.ProjectId) && state.Submitted(e.ProjectId))Render();});
            subscriptions.Subscribe<BidWonEvent>(e=>{if(Own(e.ProjectId) && state.Awarded(e.ProjectId,true))Render();});
            subscriptions.Subscribe<BidLostEvent>(e=>{if(Own(e.ProjectId) && state.Awarded(e.ProjectId,false))Render();});
            subscriptions.Subscribe<ProjectStartedEvent>(e=>{if(Own(e.ProjectId) && state.Started(e.ProjectId))Render();});
            subscriptions.Subscribe<PhaseProgressChangedEvent>(e=>{if(Own(e.ProjectId) && state.Progress(e.ProjectId,e.PhaseId,e.Progress))Render();});

            Render();
        }
        private bool Own(string id)=>dashboard.Session?.Find(id)!=null;
        public void Navigated(string screen){if(state.Navigate(screen))Render();}
        public void Reviewed(string id){if(Own(id) && state.ReviewEstimate(id))Render();}
        private void Go(){dashboard.Navigate(state.Step<3?"Bidding":state.Step==3?"Projects":"Project Detail",state.ProjectId);}
        private void Render()
        {
            title.text=state.Complete?"First phase complete":$"Getting started · {state.Step+1} / 7 · {Titles[state.Step]}";
            text.text=state.Complete?"Continue phase by phase: order supplies ahead, avoid overlapping crew bookings, and respond to alerts. At project completion, check the final profit and upcoming receipts in Finance; retention remains unpaid until the warranty release date.":Texts[state.Step];
            body.style.display=dismissed?DisplayStyle.None:DisplayStyle.Flex;toggle.text=dismissed?"Show guide":"Hide guide";
            action.text=state.Complete?"Review project":"Open relevant screen";
        }
        public TutorialProgressData Progress=>state;
        public bool Dismissed=>dismissed;
        public void Restore(int step,string project,string phase,bool hidden){state.Restore(step,project,phase);dismissed=hidden;Render();}
        public void Dispose(){subscriptions.Dispose();view.RemoveFromHierarchy();}
    }
}
