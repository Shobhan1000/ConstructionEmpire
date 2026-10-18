using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Finance
{
    /// <summary>Milestone 6 procurement plugs into this interface. Accrual and payment are separate operations.</summary>
    public interface IMaterialCostSink { string AccrueMaterialPurchase(string projectId,decimal amount,string purchaseId,long dueHour); }
    public sealed class FinanceSystem : IRegisterableSystem,IMaterialCostSink,IDisposable
    {
        private readonly EventSubscriptions subscriptions=new EventSubscriptions();
        private readonly HashSet<string> expenseReferences=new HashSet<string>(), forecastWarnings=new HashSet<string>();
        private readonly Dictionary<string,ProjectData> projects=new Dictionary<string,ProjectData>();
        private readonly Dictionary<string,decimal> estimates=new Dictionary<string,decimal>();
        private readonly GameTimeManager clock;
        internal readonly FinanceSaveData State;
        public CompanyData Company {get;}
        public FinanceSettings Settings {get;}
        public CashFlowTracker CashFlow {get;}
        public BankingSystem Banking {get;}
        public InsuranceSystem Insurance {get;}
        public TaxSystem Taxes {get;}
        public long Hour=>clock.TotalHours;
        public int HoursPerDay=>clock.CaptureState().HoursPerDay;
        public int DaysPerMonth=>clock.CaptureState().DaysPerMonth;
        public IReadOnlyList<ContractData> Contracts=>State.Contracts.AsReadOnly();
        public IReadOnlyList<LedgerEntry> Ledger=>State.Ledger.AsReadOnly();
        public IReadOnlyList<FinancialObligation> Obligations=>State.Obligations.AsReadOnly();
        public decimal Revenue=>State.Ledger.Where(x=>x.Kind==LedgerKind.Revenue).Sum(x=>x.Amount);
        public decimal Expenses=>State.Ledger.Where(x=>x.Kind==LedgerKind.Expense).Sum(x=>x.Amount);
        public decimal Profit=>Revenue-Expenses;
        public decimal NetMargin=>Revenue==0 ? 0 : Profit/Revenue;
        public decimal GrossMargin=>Revenue==0 ? 0 : (Revenue-State.Ledger.Where(x=>x.Kind==LedgerKind.Expense && !string.IsNullOrEmpty(x.ProjectId)).Sum(x=>x.Amount))/Revenue;
        public decimal AccountsReceivable=>State.Obligations.Where(x=>x.Receivable && !x.Paid).Sum(x=>x.Amount);
        public decimal AccountsPayable=>State.Obligations.Where(x=>!x.Receivable && !x.Paid).Sum(x=>x.Amount);
        public decimal UnbilledRevenue=>Math.Max(0,Revenue-State.Obligations.Where(x=>x.Receivable).Sum(x=>x.Amount));
        public FinanceSystem(CompanyData company,GameTimeManager clock,FinanceSettings settings)
        {
            Company=company??throw new ArgumentNullException(nameof(company));this.clock=clock??throw new ArgumentNullException(nameof(clock));Settings=settings??throw new ArgumentNullException(nameof(settings));settings.Validate();
            State=new FinanceSaveData {OpeningCash=(decimal)company.CashBalance,Cash=(decimal)company.CashBalance,CreditScore=settings.StartingCreditScore,LastTickHour=clock.TotalHours,LastInsuranceHour=clock.TotalHours,LastTaxHour=clock.TotalHours};
            CashFlow=new CashFlowTracker(company,State,(decimal)settings.LowCashThreshold); Banking=new BankingSystem(this); Insurance=new InsuranceSystem(this); Taxes=new TaxSystem(this);
            subscriptions.Subscribe<ConstructionWorkAccruedEvent>(OnWork);
        }
        public ContractData GetContract(string projectId)=>State.Contracts.FirstOrDefault(x=>x.ProjectId==projectId);
        public void RecordCapitalPurchase(decimal amount,string reference,string description)
        {if(amount<0 || CashFlow.Cash<amount)throw new InvalidOperationException("Insufficient cash.");Post(null,LedgerKind.CashPayment,FinancialCategory.Equipment,Money(amount),reference,description);CashFlow.Apply(-Money(amount),description);Changed();}
        public void RecordCapitalSale(decimal amount,string reference,string description)
        {if(amount<0)throw new ArgumentOutOfRangeException(nameof(amount));Post(null,LedgerKind.CashReceipt,FinancialCategory.Equipment,Money(amount),reference,description);CashFlow.Apply(Money(amount),description);Changed();}
        public decimal ProjectCosts(string id)=>State.Ledger.Where(x=>x.ProjectId==id && x.Kind==LedgerKind.Expense).Sum(x=>x.Amount);
        public ContractData RegisterContract(ProjectData project,decimal estimatedCost,ContractData contract=null)
        {
            if(project==null || estimatedCost<0) throw new ArgumentException("Project and estimate are required.");
            if(GetContract(project.Id)!=null) return GetContract(project.Id);
            if(contract==null)
            {
                contract=new ContractData {Id="contract-"+project.Id,ProjectId=project.Id,ClientId=project.ClientId,ContractValue=(decimal)project.ContractValue,
                    StartHour=Hour,CompletionDeadlineHour=project.DeadlineHour,RetentionPercentage=(decimal)Settings.RetentionFraction,WarrantyDurationHours=(long)Settings.WarrantyDays*HoursPerDay,
                    PenaltyPerLateDay=(decimal)project.PenaltyPerLateDay,MaximumPenaltyFraction=(decimal)Settings.MaximumPenaltyFraction,ChangeOrderRulesReference="standard-written-approval",
                    RequiredQuality=project.QualityRequirement==QualityTier.Premium ? .9 : project.QualityRequirement==QualityTier.Enhanced ? .75 : .6};
                if(Settings.UpfrontFraction>0) contract.PaymentSchedule.Add(new ContractPayment {Id="upfront",Stage=PaymentStage.Upfront,Fraction=(decimal)Settings.UpfrontFraction});
                if(Settings.ProgressFraction>0) contract.PaymentSchedule.Add(new ContractPayment {Id="progress",Stage=PaymentStage.Progress,Fraction=(decimal)Settings.ProgressFraction,ProgressThreshold=Settings.ProgressPaymentThreshold,DelayHours=Settings.ProgressPaymentDelayDays*HoursPerDay});
                contract.PaymentSchedule.Add(new ContractPayment {Id="final",Stage=PaymentStage.Final,Fraction=1m-(decimal)Settings.UpfrontFraction-(decimal)Settings.ProgressFraction,ProgressThreshold=100,DelayHours=Settings.FinalPaymentDelayDays*HoursPerDay});
            }
            contract.Validate(); if(contract.ProjectId!=project.Id) throw new ArgumentException("Contract project mismatch.");
            projects.Add(project.Id,project); estimates.Add(project.Id,estimatedCost); State.Contracts.Add(contract); Changed(project.Id); return contract;
        }
        public void StartContract(string projectId)
        {
            var contract=Require(projectId); if(contract.Started) return; contract.Started=true;contract.StartHour=Hour;
            InvoiceEligible(contract); PayDue(); Changed(projectId);
        }
        public void UpdateProgress(string projectId,double progress)
        {
            var contract=Require(projectId);
            if(double.IsNaN(progress) || progress<contract.Progress || progress>100) throw new ArgumentOutOfRangeException(nameof(progress));
            contract.Progress=progress;
            decimal earned=Money(contract.ContractValue*(decimal)progress/100m); decimal delta=earned-contract.RevenueRecognized;
            if(delta>0) { contract.RevenueRecognized=earned; Post(projectId,LedgerKind.Revenue,FinancialCategory.Contract,delta,"earned-"+projectId,"Work earned; receipt timing is separate"); }
            InvoiceEligible(contract); PayDue();
            if(Forecast(projectId).ForecastProfit<0 && forecastWarnings.Add(projectId)) GameEvents.Publish(new ProfitabilityWarningEvent(projectId,Forecast(projectId).ForecastCost,contract.ContractValue));
            Changed(projectId);
        }
        public ProjectProfitability Forecast(string projectId)
        {
            var contract=Require(projectId); decimal cost=ProjectCosts(projectId);
            // Baseline cost-to-complete avoids treating upfront material purchasing as recurring work.
            decimal remaining=contract.Completed ? 0 : estimates[projectId]*(1m-(decimal)contract.Progress/100m);
            decimal extra=State.Ledger.Where(x=>x.ProjectId==projectId && x.Kind==LedgerKind.Revenue && x.Category!=FinancialCategory.Contract).Sum(x=>x.Amount);
            return new ProjectProfitability(cost,remaining,contract.RevenueRecognized+extra,contract.ContractValue+extra,(decimal)projects[projectId].Budget);
        }
        public void AmendScope(string projectId,decimal price,decimal estimatedCost,long deadline)
        {var contract=Require(projectId);if(contract.Completed || price<0 || estimatedCost<0 || deadline<contract.CompletionDeadlineHour)throw new ArgumentException("Invalid scope amendment.");contract.ContractValue+=price;contract.CompletionDeadlineHour=deadline;estimates[projectId]+=estimatedCost;Changed(projectId);}
        public void CompleteContract(string projectId,double quality)
        {
            var contract=Require(projectId); if(contract.Completed) return;
            if(double.IsNaN(quality) || quality<0 || quality>1) throw new ArgumentOutOfRangeException(nameof(quality));
            contract.Completed=true;contract.CompletionHour=Hour;contract.QualityOutcome=quality;
            UpdateProgress(projectId,100);
            if(Hour>contract.CompletionDeadlineHour)
            {
                decimal days=(decimal)Math.Ceiling((Hour-contract.CompletionDeadlineHour)/(double)HoursPerDay);
                AccrueExpense(projectId,Math.Min(contract.ContractValue*contract.MaximumPenaltyFraction,days*contract.PenaltyPerLateDay),FinancialCategory.Penalty,"penalty-"+contract.Id,"Contract late-completion penalty",Hour);
            }
            if(!contract.RetentionInvoiced && contract.RetentionAmount>0)
            { contract.RetentionInvoiced=true; Invoice(projectId,contract.RetentionAmount,FinancialCategory.Retention,"retention-"+contract.Id,"Warranty retention",contract.RetentionReleaseHour); }
            ProjectProfitabilityCalculator.ApplyCompletionReputation(Company,contract,Forecast(projectId).ForecastProfit,Settings);
            PayDue();Changed(projectId);
        }
        private void InvoiceEligible(ContractData contract)
        {
            if(!contract.Started) return;
            foreach(var payment in contract.PaymentSchedule)
            {
                if(payment.Invoiced || (payment.Stage==PaymentStage.Progress && contract.Progress<payment.ProgressThreshold) || (payment.Stage==PaymentStage.Final && !contract.Completed)) continue;
                decimal amount=Money(contract.ContractValue*payment.Fraction*(1-contract.RetentionPercentage));
                // Last milestone absorbs cent-rounding so paid + retention always equals contract value.
                if(payment.Stage==PaymentStage.Final && contract.PaymentSchedule.Count(x=>!x.Invoiced)==1)
                    amount=contract.ContractValue-contract.RetentionAmount-State.Obligations.Where(x=>x.Receivable && x.ProjectId==contract.ProjectId && contract.PaymentSchedule.Any(p=>x.Id==contract.Id+"-"+p.Id)).Sum(x=>x.Amount);
                payment.Invoiced=true;
                Invoice(contract.ProjectId,amount,payment.Stage==PaymentStage.Final ? FinancialCategory.FinalPayment : FinancialCategory.ProgressPayment,contract.Id+"-"+payment.Id,payment.Stage+" payment",checked(Hour+payment.DelayHours));
            }
        }
        public string AccrueMaterialPurchase(string projectId,decimal amount,string purchaseId,long dueHour)=>AccrueExpense(projectId,amount,FinancialCategory.Materials,purchaseId,"Material purchase",dueHour);
        public string AccrueExpense(string projectId,decimal amount,FinancialCategory category,string reference,string description,long dueHour,bool nonCash=false)
        {
            if(amount<0 || dueHour<0 || string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Invalid expense.");
            if(!expenseReferences.Add(reference) || amount==0) return reference;
            amount=Money(amount); Post(projectId,LedgerKind.Expense,category,amount,reference,description);
            if(!nonCash) State.Obligations.Add(new FinancialObligation {Id=reference,ProjectId=projectId,Amount=amount,Category=category,Description=description,DueHour=dueHour});
            PayDue(); Changed(projectId); return reference;
        }
        public void RecordExtraRevenue(string projectId,decimal amount,FinancialCategory category,string reference,long dueHour)
        {
            if(amount<=0 || (category!=FinancialCategory.ChangeOrder && category!=FinancialCategory.Bonus)) throw new ArgumentException("Only approved positive change orders or bonuses.");
            if(State.Ledger.Any(x=>x.Kind==LedgerKind.Revenue && x.Reference==reference)) return;
            Post(projectId,LedgerKind.Revenue,category,Money(amount),reference,category.ToString()); Invoice(projectId,amount,category,reference,category.ToString(),dueHour); PayDue();Changed(projectId);
        }
        internal void RecordInsuranceSettlement(string projectId,decimal amount,string reference,long dueHour)
        {Post(projectId,LedgerKind.Revenue,FinancialCategory.Insurance,Money(amount),reference,"Approved insurance claim");Invoice(projectId,amount,FinancialCategory.Insurance,reference,"Insurance settlement",dueHour);Changed(projectId);}
        private void Invoice(string projectId,decimal amount,FinancialCategory category,string reference,string description,long dueHour)
        {
            if(amount<=0) return;
            if(State.Obligations.Any(x=>x.Receivable && x.Id==reference)) return;
            State.Obligations.Add(new FinancialObligation {Id=reference,ProjectId=projectId,Amount=Money(amount),Category=category,Description=description,DueHour=dueHour,Receivable=true});
        }
        private void PayDue()
        {
            foreach(var item in State.Obligations.Where(x=>!x.Paid && x.DueHour<=Hour).OrderBy(x=>x.DueHour).ThenBy(x=>x.Id,StringComparer.Ordinal).ToArray())
            {
                item.Paid=true;
                Post(item.ProjectId,item.Receivable ? LedgerKind.CashReceipt : LedgerKind.CashPayment,item.Category,item.Amount,item.Id,item.Description);
                CashFlow.Apply(item.Receivable ? item.Amount : -item.Amount,item.Description);
                if(item.Receivable) GameEvents.Publish(new PaymentReceivedEvent(item.ProjectId,item.Id,(float)item.Amount));
            }
        }
        private void OnWork(ConstructionWorkAccruedEvent e)
        {
            if(GetContract(e.ProjectId)==null) return;
            string id="work-"+(State.Sequence+1); long cadence=(long)Settings.WagePaymentDays*HoursPerDay;
            long payday=cadence==0 ? Hour : checked((Hour/cadence+1)*cadence);
            AccrueExpense(e.ProjectId,(decimal)e.LaborCost,FinancialCategory.Labor,id+"-labor","Crew wages accrued",payday);
            AccrueExpense(e.ProjectId,(decimal)e.EquipmentOperatingCost,FinancialCategory.Equipment,id+"-equipment","Equipment operating cost",payday);
            if(e.Equipment!=null)
                foreach(var machine in e.Equipment)
                    AccrueExpense(e.ProjectId,(decimal)(machine.IsRented ? (machine.DailyRentalContract?0:machine.RentalPricePerDay/HoursPerDay*e.HoursWorked) : machine.PurchasePrice/Settings.EquipmentUsefulLifeHours*e.HoursWorked),
                        machine.IsRented ? FinancialCategory.Rental : FinancialCategory.Equipment,id+"-ownership-"+machine.Id,
                        machine.IsRented ? "Equipment rental" : "Usage-based equipment depreciation",payday,!machine.IsRented);
            AccrueExpense(e.ProjectId,(decimal)(e.PhaseCost+e.HoursWorked*Settings.ProjectOverheadPerWorkHour),FinancialCategory.Office,id+"-overhead","Project overhead allocation",payday);
        }
        public void OnTick(TickContext context)
        {
            if(context.TotalHours<=State.LastTickHour) return;
            State.LastTickHour=context.TotalHours;
            if(context.StartsDay) AccrueExpense(null,(decimal)Settings.OfficeCostPerDay,FinancialCategory.Office,"office-"+Hour,"Daily office costs",Hour);
            Banking.OnTick(context);Insurance.OnTick(context);Taxes.OnTick(context);PayDue();Changed();
        }
        public void DelayNextPayment(string projectId,int delayDays)
        {
            if(delayDays<1) throw new ArgumentOutOfRangeException(nameof(delayDays));
            var next=State.Obligations.Where(x=>x.Receivable && !x.Paid && x.ProjectId==projectId && x.Category!=FinancialCategory.Retention).OrderBy(x=>x.DueHour).FirstOrDefault();
            if(next==null) throw new InvalidOperationException("No pending non-retention payment to delay.");
            next.DueHour=checked(Math.Max(Hour,next.DueHour)+(long)delayDays*HoursPerDay);Changed(projectId);
        }
        internal void Post(string project,LedgerKind kind,FinancialCategory category,decimal amount,string reference,string description)
        { State.Ledger.Add(new LedgerEntry {Sequence=checked(++State.Sequence),Hour=Hour,ProjectId=project,Kind=kind,Category=category,Amount=amount,Reference=reference,Description=description}); }
        internal void Changed(string id=null)=>GameEvents.Publish(new FinanceChangedEvent(Company,id));
        internal static decimal Money(decimal value)=>decimal.Round(value,2,MidpointRounding.AwayFromZero);
        private ContractData Require(string id)=>GetContract(id)??throw new ArgumentException("Unknown contract: "+id);
        public void Dispose() { subscriptions.Dispose();CashFlow.Dispose(); }
    }
}
