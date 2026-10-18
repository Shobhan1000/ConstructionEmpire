using System;
using System.Linq;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Finance
{
    public sealed class BankingSystem
    {
        private readonly FinanceSystem finance;
        public BankingSystem(FinanceSystem finance) { this.finance=finance; }
        public decimal Debt=>finance.State.Loans.Sum(x=>x.OutstandingPrincipal);
        public int CreditScore=>finance.State.CreditScore;
        public decimal CreditLimit=>(decimal)finance.Settings.CreditLimit*Math.Max(0,CreditScore)/1000m;
        public decimal AvailableCredit=>Math.Max(0,CreditLimit-Debt);
        public decimal MarketInterestAdjustment {get;set;}
        public decimal QuotedAnnualRate=>Math.Max(0,MarketInterestAdjustment+(decimal)finance.Settings.BaseAnnualInterestRate+(1-CreditScore/1000m)*(decimal)finance.Settings.PoorCreditRatePremium);
        public LoanData DrawLoan(decimal amount,int termMonths,string collateral,decimal collateralValue)
        {
            amount=FinanceSystem.Money(amount);
            if(amount<=0 || termMonths<1 || termMonths>120 || amount>AvailableCredit || CreditScore<finance.Settings.MinimumCreditScore) throw new InvalidOperationException("Loan exceeds credit eligibility, limit or supported term (1–120 months).");
            if(string.IsNullOrWhiteSpace(collateral) || collateralValue<amount*(decimal)finance.Settings.CollateralRequiredFraction) throw new InvalidOperationException("Insufficient collateral for this loan.");
            var loan=new LoanData {Id="loan-"+(finance.State.Loans.Count+1),Principal=amount,OutstandingPrincipal=amount,AnnualInterestRate=QuotedAnnualRate,
                TermMonths=termMonths,MonthlyPrincipal=FinanceSystem.Money(amount/termMonths),Collateral=collateral,
                NextPaymentHour=checked(finance.Hour+(long)finance.DaysPerMonth*finance.HoursPerDay)};
            finance.State.Loans.Add(loan);finance.Post(null,LedgerKind.LoanDraw,FinancialCategory.Financing,amount,loan.Id,"Loan drawn against "+collateral);
            finance.CashFlow.Apply(amount,"Loan proceeds");finance.Changed();return loan;
        }
        public decimal NextMonthlyPayment(LoanData loan)=>Math.Min(loan.OutstandingPrincipal,loan.MonthlyPrincipal)+FinanceSystem.Money(loan.OutstandingPrincipal*loan.AnnualInterestRate/12m);
        public void OnTick(TickContext context)
        {
            foreach(var loan in finance.State.Loans.Where(x=>x.OutstandingPrincipal>0 && x.NextPaymentHour<=context.TotalHours).ToArray())
            {
                decimal interest=FinanceSystem.Money(loan.OutstandingPrincipal*loan.AnnualInterestRate/12m);
                decimal principal=loan.PaymentsMade+1>=loan.TermMonths ? loan.OutstandingPrincipal : Math.Min(loan.OutstandingPrincipal,loan.MonthlyPrincipal);
                string reference=loan.Id+"-payment-"+(loan.PaymentsMade+1);
                finance.AccrueExpense(null,interest,FinancialCategory.Financing,reference+"-interest","Monthly loan interest",finance.Hour);
                loan.OutstandingPrincipal-=principal;loan.PaymentsMade++;loan.NextPaymentHour=checked(loan.NextPaymentHour+(long)finance.DaysPerMonth*finance.HoursPerDay);
                finance.Post(null,LedgerKind.PrincipalRepayment,FinancialCategory.Financing,principal,reference,"Loan principal repayment");
                finance.CashFlow.Apply(-principal,"Loan principal repayment");
                finance.State.CreditScore=Math.Max(0,Math.Min(1000,CreditScore+(finance.CashFlow.IsOverdrawn ? -25 : 2)));
                finance.Changed();
            }
        }
    }
}
