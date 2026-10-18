using System;
using System.Linq;
using System.Collections.Generic;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Management
{
    public sealed class CompanyDevelopmentSystem:IDisposable,IRegisterableSystem
    {
        private readonly ManagementSession session;private readonly EventSubscriptions subscriptions=new EventSubscriptions();
        private readonly Dictionary<string,ClientRelationshipData> clients=new Dictionary<string,ClientRelationshipData>();
        private readonly HashSet<string> completed=new HashSet<string>();private int nextEquipment;
        public DevelopmentSettings Settings {get;}public int Level {get;private set;}=1;
        public string LevelName=>Settings.Levels[Level-1].Name;
        public IReadOnlyList<ClientRelationshipData> Clients=>clients.Values.OrderBy(x=>x.Name).ToList();
        public int CompletedProjects=>session.Projects.Count(x=>x.Status==ProjectStatus.Complete);
        public double Valuation=>Math.Max(0,(double)session.Finance.CashFlow.Cash+(double)session.Finance.AccountsReceivable-(double)session.Finance.AccountsPayable-(double)session.Finance.Banking.Debt+session.Workforce.Equipment.Where(x=>!x.IsRented && x.Status!=EquipmentStatus.Retired).Sum(x=>x.PurchasePrice*x.Condition*Settings.ResaleValueFraction));
        public CompanyDevelopmentSystem(ManagementSession session,DevelopmentSettings settings)
        {
            this.session=session;Settings=settings??new DevelopmentSettings();Settings.Validate();EnsureClients();
            subscriptions.Subscribe<ProjectCompletedEvent>(e=>{var p=session.Find(e.ProjectId);if(p==null || p.Status!=ProjectStatus.Complete || !completed.Add(p.Id))return;var c=Client(p);c.CompletedProjects++;c.Relationship=Math.Max(0,Math.Min(1,c.Relationship+(session.Simulation.Time.TotalHours<=p.DeadlineHour?Settings.SuccessfulRelationshipGain:-Settings.DelayedRelationshipPenalty)-(p.Site.Quality<.6?Settings.PoorQualityPenalty:0)));Refresh();Changed("Client review received: "+c.Name);});
            subscriptions.Subscribe<FinanceChangedEvent>(e=>{if(ReferenceEquals(e.Company,session.Simulation.Company))Refresh();});
            subscriptions.Subscribe<ReputationChangedEvent>(e=>{if(ReferenceEquals(e.Company,session.Simulation.Company))Refresh();});
            session.Simulation.Register(this);if(Settings.StartWithRegularShifts)foreach(var crew in session.Expansion.Crews)SetWorkPolicy(crew.Id,false,false);Refresh();
        }
        private void EnsureClients(){foreach(var p in session.Projects)Client(p);}
        public ClientRelationshipData Client(ProjectData p)
        {if(!clients.TryGetValue(p.ClientId,out var c)){int hash=0;foreach(char ch in p.ClientId)hash=(hash+ch)%3;c=new ClientRelationshipData {Id=p.ClientId,Name=p.ClientName,Preference=new[]{"Value conscious","Quality focused","Deadline focused"}[hash]};clients.Add(c.Id,c);}return c;}
        public void Refresh()
        {
            EnsureClients();int next=CalculateLevel(Settings,CompletedProjects,session.Revenue,session.Simulation.Company.Reputation);
            if(next<=Level)return;Level=next;session.Research.System.SetCompanyLevel(Level);Changed("Company promoted to "+LevelName);
        }
        public static int CalculateLevel(DevelopmentSettings settings,int completed,double revenue,double reputation)
        {int level=1;for(int i=1;i<settings.Levels.Count;i++){var l=settings.Levels[i];if(completed>=l.CompletedProjects && revenue>=l.EarnedRevenue && reputation>=l.Reputation)level=i+1;else break;}return level;}
        public void AcquireEquipment(string definition,bool rented)
        {
            var template=Settings.EquipmentCatalog.SingleOrDefault(x=>x.DefinitionId==definition)??throw new ArgumentException("Unknown equipment.");
            decimal price=(decimal)(rented?template.RentalPricePerDay:template.PurchasePrice);if(session.Finance.CashFlow.Cash<price)throw new InvalidOperationException("Insufficient cash for this equipment.");
            var item=template.Copy();item.Id="acquired-equipment-"+(++nextEquipment);item.IsRented=rented;item.DailyRentalContract=rented;item.Status=EquipmentStatus.Available;item.CurrentProjectId=null;item.CurrentTaskId=null;item.Location=session.Settings.OfficeLocation;
            if(rented)session.Finance.AccrueExpense(null,price,FinancialCategory.Rental,item.Id+"-first-day","First day rental: "+item.Name,session.Finance.Hour);
            else session.Finance.RecordCapitalPurchase(price,item.Id,"Equipment purchase: "+item.Name);
            session.Workforce.Equipment.Add(item);session.Research.Apply();GameEvents.Publish(new ResourcesChangedEvent(null));Changed("Equipment acquired");
        }
        public void ReleaseEquipment(string id)
        {
            var item=session.Workforce.Equipment.Single(x=>x.Id==id);
            if(item.CurrentProjectId!=null || session.Scheduling.Bookings.Any(x=>x.Open && x.Equipment.Any(e=>e.Id==id)))throw new InvalidOperationException("Release all equipment bookings first.");
            if(item.Status==EquipmentStatus.Retired)throw new InvalidOperationException("Equipment already removed.");
            if(!item.IsRented)session.Finance.RecordCapitalSale((decimal)(item.PurchasePrice*item.Condition*Settings.ResaleValueFraction),"sale-"+id,"Equipment sold: "+item.Name);
            item.Status=EquipmentStatus.Retired;item.DailyRentalContract=false;GameEvents.Publish(new ResourcesChangedEvent(null));Changed("Equipment removed");
        }
        public void SetWorkPolicy(string crewId,bool overtime,bool weekends)
        {var c=session.Expansion.Crews.Single(x=>x.Id==crewId);c.UseShiftPolicy=true;c.ShiftStartHour=Settings.ShiftStartHour;c.ShiftHours=overtime?Settings.OvertimeShiftHours:Settings.StandardShiftHours;c.WorkWeekends=weekends;c.OvertimeMultiplier=overtime?Settings.OvertimeWageMultiplier:1;GameEvents.Publish(new ResourcesChangedEvent(c.CurrentProjectId));Changed("Crew working hours updated");}
        public void OnTick(TickContext context)
        {if(!context.StartsDay)return;foreach(var item in session.Workforce.Equipment.Where(x=>x.DailyRentalContract && x.Status!=EquipmentStatus.Retired))session.Finance.AccrueExpense(null,(decimal)item.RentalPricePerDay,FinancialCategory.Rental,"rental-"+item.Id+"-"+context.TotalHours,"Daily rental: "+item.Name,context.TotalHours);Refresh();}
        private void Changed(string message)=>GameEvents.Publish(new CompanyDevelopmentChangedEvent(this,message));
        public void Dispose(){subscriptions.Dispose();session.Simulation.Unregister(this);}
    }
}
