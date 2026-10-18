using ConstructionEmpire.Data.Save;
using System;
using System.Linq;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal static class FinanceScreenPresenter
    {
        private static string FinanceTip(string name)
        {
            switch(name){case "Cash on hand":return "cash";case "Cash movement":return "cash-flow";case "Earned revenue":return "revenue";case "Accrued expenses":case "Accrued costs":return "expenses";case "Profit":case "Current profit":return "profit";case "Forecast profit":return "expected-profit";case "Warranty retention":return "retention";case "Accounts receivable":return "accounts-receivable";case "Accounts payable":return "accounts-payable";case "Debt":return "debt";case "Forecast cost":return "forecast";default:return null;}
        }
        public static void Build(VisualElement root,MainDashboardController dashboard)
        {
            DashboardElements.Label(root,"Profit measures earned work minus accrued costs. Cash changes only when money is received or paid.","form-note");
            DashboardElements.Box(root,"card","finance-live"); Refresh(root,dashboard);
            Insurance(root,dashboard);
            DashboardElements.Label(root,"Working capital loan","section-title");
            var amount=new FloatField("Amount") {value=50000}; var collateral=new TextField("Collateral description") {value="Owned fleet"};
            var value=new FloatField("Collateral value") {value=(float)dashboard.Session.Workforce.Equipment.Sum(x=>x.PurchasePrice)};
            root.Add(amount);root.Add(collateral);root.Add(value);
            DashboardElements.Button(root,"Review loan",()=>dashboard.AskConfirmation("Borrow "+DashboardElements.Money(amount.value)+" over "+dashboard.Session.Finance.Settings.LoanTermMonths+" months at "+dashboard.Session.Finance.Banking.QuotedAnnualRate.ToString("P1")+" annual interest?",()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.DrawLoan,Amount=amount.value,Start=dashboard.Session.Finance.Settings.LoanTermMonths,Text=collateral.value,Value=value.value})),"primary");
        }
        public static void Refresh(VisualElement root,MainDashboardController dashboard)
        {
            var live=root.Q("finance-live"); if(live==null) return;live.Clear(); var f=dashboard.Session.Finance;
            foreach(var pair in new[]{("Cash on hand",f.CashFlow.Cash),("Earned revenue",f.Revenue),("Accrued expenses",f.Expenses),("Profit",f.Profit),("Cash movement",f.CashFlow.NetCashFlow),("Accounts receivable",f.AccountsReceivable),("Accounts payable",f.AccountsPayable),("Unbilled earned revenue",f.UnbilledRevenue),("Debt",f.Banking.Debt),("Available credit",f.Banking.AvailableCredit),("Estimated tax",f.Taxes.EstimatedTaxes),("Tax due",f.Taxes.TaxDue),("Tax reserve",f.Taxes.TaxReserve)})
                DashboardElements.Label(live,pair.Item1+": "+DashboardElements.Money((double)pair.Item2),"table-row").tooltip=DashboardTooltips.For(FinanceTip(pair.Item1));
            DashboardElements.Label(live,$"Gross margin {f.GrossMargin:P1} · Net margin {f.NetMargin:P1} · Credit {f.Banking.CreditScore} · Loan rate {f.Banking.QuotedAnnualRate:P1}","form-note");
            DashboardElements.Label(live,$"Insurance premium {f.Insurance.Premium:C0} every {f.Settings.InsuranceCadenceDays} days. Abstract tax rate {f.Settings.TaxRate:P0} assessed every {f.Settings.TaxCadenceDays} days.","form-note");
            DashboardElements.Label(live,"Upcoming receipts and bills","section-title");
            foreach(var item in f.Obligations.Where(x=>!x.Paid).OrderBy(x=>x.DueHour)) DashboardElements.Label(live,$"Hour {item.DueHour} · {(item.Receivable ? "Receive" : "Pay")} {DashboardElements.Money((double)item.Amount)} · {item.Description} · {item.ProjectId??"Company"}","table-row");
            DashboardElements.Label(live,"Recent ledger entries","section-title");
            foreach(var item in f.Ledger.Reverse().Take(20)) DashboardElements.Label(live,$"Hour {item.Hour} · {item.Kind} · {item.Category} · {DashboardElements.Money((double)item.Amount)} · {item.Description}","table-row");
        }
        public static void Project(VisualElement root,MainDashboardController dashboard,string id)
        {
            var f=dashboard.Session.Finance;var c=f.GetContract(id);
            if(c==null) { DashboardElements.Label(root,"A contract is created when this opportunity is won.","empty-state");return; }
            ChangeOrders(root,dashboard,id);
            var result=f.Forecast(id);
            foreach(var pair in new[]{("Contract",c.ContractValue),("Earned revenue",c.RevenueRecognized),("Accrued costs",result.CurrentCosts),("Remaining estimate",result.RemainingEstimatedCosts),("Forecast cost",result.ForecastCost),("Current profit",result.CurrentProfit),("Forecast profit",result.ForecastProfit),("Budget variance",result.BudgetVariance),("Warranty retention",c.RetentionAmount)}) DashboardElements.Label(root,pair.Item1+": "+DashboardElements.Money((double)pair.Item2),"table-row").tooltip=DashboardTooltips.For(FinanceTip(pair.Item1));
            DashboardElements.Label(root,$"Start hour {c.StartHour} · Deadline {c.CompletionDeadlineHour} · Warranty {c.WarrantyDurationHours} hours · Quality target {c.RequiredQuality:P0}","form-note");
            DashboardElements.Label(root,c.Completed ? "Retention release: hour "+c.RetentionReleaseHour : "Retention release: warranty period after completion","form-note");
            foreach(var payment in c.PaymentSchedule) DashboardElements.Label(root,$"{payment.Stage} · {payment.Fraction:P0} before retention · Progress threshold {payment.ProgressThreshold:0}% · Delay {payment.DelayHours} hours · {(payment.Invoiced ? "Invoiced" : "Not yet due")}","table-row");
            foreach(var item in f.Obligations.Where(x=>x.ProjectId==id)) DashboardElements.Label(root,$"{item.Description} · {DashboardElements.Money((double)item.Amount)} · Hour {item.DueHour} · {(item.Paid ? "Paid" : "Pending")}","table-row");
        }

        private static void Insurance(VisualElement root,MainDashboardController dashboard)
        {
            var insurance=dashboard.Session.Finance.Insurance;DashboardElements.Label(root,"Insurance coverage","section-title");
            foreach(ConstructionEmpire.Systems.Finance.InsuranceType type in Enum.GetValues(typeof(ConstructionEmpire.Systems.Finance.InsuranceType)))
            {var policy=type;var toggle=new Toggle(type+" · "+DashboardElements.Money((double)insurance.PolicyPremium(type))+" per billing period"){value=insurance.HasCoverage(type)};root.Add(toggle);toggle.RegisterValueChangedCallback(e=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.SetInsurance,Start=(int)policy,Flag=e.newValue})));}
            DashboardElements.Label(root,"New coverage charges a prorated premium. Claims apply the deductible and coverage limit, then arrive after the settlement delay; cancellation has no refund.","form-note");
        }
        private static void ChangeOrders(VisualElement root,MainDashboardController dashboard,string id)
        {
            var project=dashboard.Session.Find(id);if(project.Status!=ConstructionEmpire.Data.Bidding.ProjectStatus.Active)return;
            DashboardElements.Label(root,"Scope changes","section-title");
            var proposal=dashboard.Session.ChangeOrders.Orders.FirstOrDefault(x=>x.ProjectId==id);
            if(proposal==null){DashboardElements.Button(root,"Review client's optional scope request",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.ProposeChangeOrder,Id=id})));return;}
            DashboardElements.Label(root,proposal.Description+" · "+proposal.Status,"form-note");
            DashboardElements.Label(root,"Offer "+DashboardElements.Money(proposal.OfferedPrice)+" · added estimated cost "+DashboardElements.Money(proposal.EstimatedCost)+" · deadline extension "+proposal.ExtensionHours+" hours","form-note");
            if(proposal.Status!=ConstructionEmpire.Data.Company.ChangeOrderStatus.Proposed)return;
            DashboardElements.Button(root,"Accept written amendment",()=>dashboard.AskConfirmation("Accept added scope, price and deadline? Materials and labor requirements will increase.",()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.DecideChangeOrder,Id=proposal.Id,Flag=true})));
            DashboardElements.Button(root,"Decline",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.DecideChangeOrder,Id=proposal.Id})));
            DashboardElements.Button(root,"Negotiate higher price",()=>dashboard.AskConfirmation("Ask for a higher price? The client may withdraw this optional request.",()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.NegotiateChangeOrder,Id=proposal.Id}))).SetEnabled(!proposal.Negotiated);
        }
    }
}
