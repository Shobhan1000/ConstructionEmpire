using System;
using System.Linq;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Time;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal sealed class SessionMenuPresenter:IDisposable
    {
        private readonly MainDashboardController dashboard;
        private readonly VisualElement overlay,body;
        private readonly VisualElement[] background;
        private readonly Label status;
        private readonly EventSubscriptions subscriptions=new EventSubscriptions();
        private bool entered;
        public SessionMenuPresenter(MainDashboardController owner,VisualElement root)
        {
            dashboard=owner;
            DashboardElements.Button(root.Q("time-controls").parent,"Game menu",Open);
            background=root.Children().ToArray();
            overlay=new VisualElement {name="session-menu"};root.Add(overlay);
            overlay.style.position=Position.Absolute;overlay.style.left=0;overlay.style.right=0;overlay.style.top=0;overlay.style.bottom=0;
            overlay.style.backgroundColor=new Color(.04f,.07f,.12f,.96f);overlay.style.alignItems=Align.Center;overlay.style.justifyContent=Justify.Center;
            var card=DashboardElements.Box(overlay,"card");card.style.width=650;card.style.maxWidth=Length.Percent(94);card.style.maxHeight=new StyleLength(new Length(95,LengthUnit.Percent));
            DashboardElements.Label(card,"CONSTRUCTION EMPIRE","page-title");
            DashboardElements.Label(card,"Build a company. Balance your commitments.","form-note");
            status=DashboardElements.Label(card,"Choose a new company or load a saved game.","form-note");
            var scroll=new ScrollView();card.Add(scroll);body=scroll;
            subscriptions.Subscribe<SessionStorageChangedEvent>(e=>{if(!ReferenceEquals(e.Source,dashboard.Bootstrap))return;status.text=e.Message;body.SetEnabled(!e.Loading);});
            subscriptions.Subscribe<SimulationReplacedEvent>(e=>{if(ReferenceEquals(e.Source,dashboard.Bootstrap)){entered=true;Render();}});
            Open();
        }
        public void Open(){dashboard.Bootstrap.GameTime.Speed=SimulationSpeed.Pause;overlay.style.display=DisplayStyle.Flex;foreach(var item in background)item.SetEnabled(false);Render();}
        private void Close(){overlay.style.display=DisplayStyle.None;foreach(var item in background)item.SetEnabled(true);}
        private void Render()
        {
            body.Clear();var bootstrap=dashboard.Bootstrap;
            DashboardElements.Button(body,entered?"Resume company":"Begin with this company",()=>{entered=true;Close();},"primary");
            if(bootstrap.MostRecentSaveSlot>=0)
                DashboardElements.Button(body,"Continue latest saved company",()=>Confirm("Load your latest save? Unsaved changes will be discarded after validation.",()=>bootstrap.LoadGame(bootstrap.MostRecentSaveSlot)),"primary");
            if(bootstrap.HasSave(0,true))DashboardElements.Button(body,"Recover autosave backup",()=>Confirm("Recover the previous autosave?",()=>bootstrap.LoadGame(0,true)));
            DashboardElements.Button(body,"Start a new company",NewCareer);
            for(int slot=1;slot<=3;slot++)
            {
                int chosen=slot;var row=DashboardElements.Box(body,"action-row");DashboardElements.Label(row,"Slot "+slot);
                DashboardElements.Button(row,"Save",()=>Confirm("Save current company to slot "+chosen+"? The previous file becomes its backup.",()=>bootstrap.SaveGame(chosen)));
                DashboardElements.Button(row,"Load",()=>Confirm("Load slot "+chosen+"? Unsaved changes will be discarded after validation.",()=>bootstrap.LoadGame(chosen))).SetEnabled(bootstrap.HasSave(slot));
                DashboardElements.Button(row,"Recover backup",()=>Confirm("Recover the previous save in slot "+chosen+"?",()=>bootstrap.LoadGame(chosen,true))).SetEnabled(bootstrap.HasSave(slot,true));
            }
            DashboardElements.Button(body,"Help and field guide",()=>{Close();dashboard.OpenHelp();});
            DashboardElements.Button(body,"Settings",()=>{Close();dashboard.Navigate("Settings");});
            DashboardElements.Button(body,"Save and quit",()=>Act(()=>{bootstrap.SaveGame(0);Application.Quit();}));
            DashboardElements.Label(body,"Autosaves use a separate slot every five minutes of play. Save and quit also uses this slot. Manual slots stay untouched. Loaded games start paused.","form-note");
        }
        private void NewCareer()
        {
            body.Clear();DashboardElements.Label(body,"Choose your starting market","card-title");
            DashboardElements.Label(body,"Starting over replaces this unsaved session. Existing save slots are kept. Regional prices are fictional and all offices share one GBP ledger.","form-note");
            var name=new TextField("Company name") {value="New Horizon Construction"};body.Add(name);
            var regions=ConstructionEmpire.Data.Expansion.WorldAtlasFactory.Create();
            var country=new DropdownField("Country",regions.Select(r=>r.CountryName).Distinct().ToList(),0);body.Add(country);
            var city=new DropdownField("Starting city");body.Add(city);
            var seed=new IntegerField("World seed") {value=12345};body.Add(seed);seed.tooltip="The same seed and the same decisions reproduce the same simulation.";
            var capital=new FloatField("Starting cash (GBP)") {value=250000};body.Add(capital);
            var summary=DashboardElements.Label(body,"","form-note");
            void Describe(){var region=regions.First(r=>r.CountryName==country.value && r.Name==city.value);summary.text=region.MarketDescription+" Operating cost "+region.CostFactor.ToString("0.00")+"×. Your first office is established here with the starter workforce and five local jobs.";}
            void Cities(){city.choices=regions.Where(r=>r.CountryName==country.value).Select(r=>r.Name).ToList();city.SetValueWithoutNotify(city.choices[0]);Describe();}
            country.RegisterValueChangedCallback(_=>Cities());city.RegisterValueChangedCallback(_=>Describe());Cities();
            DashboardElements.Button(body,"Create company",()=>{var options=new ConstructionEmpire.Data.Expansion.CareerOptions {CompanyName=name.value,RegionId=regions.First(r=>r.CountryName==country.value && r.Name==city.value).Id,Seed=seed.value,StartingCash=capital.value};Confirm("Create "+name.value+" in "+city.value+"? Save your current company first if you want to keep its unsaved progress.",()=>dashboard.Bootstrap.NewGame(options));},"primary");
            DashboardElements.Button(body,"Back",Render);
        }
        private void Confirm(string text,Action action)
        {
            body.Clear();DashboardElements.Label(body,text,"form-note");
            DashboardElements.Button(body,"Confirm",()=>Act(action),"primary");DashboardElements.Button(body,"Cancel",Render);
        }
        private void Act(Action action){try{action();if(!dashboard.Bootstrap.IsLoading)Render();}catch(Exception e){status.text=e.Message;Render();}}
        public void Dispose(){subscriptions.Dispose();overlay.RemoveFromHierarchy();}
    }
}
