using System;
using System.Linq;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Finance
{
    /// <summary>Abstract game tax rules, unrelated to any real jurisdiction.</summary>
    public sealed class TaxSystem
    {
        private readonly FinanceSystem finance;
        public TaxSystem(FinanceSystem finance) { this.finance=finance; }
        public decimal PretaxProfit=>finance.Profit+finance.State.Ledger.Where(x=>x.Kind==LedgerKind.Expense && x.Category==FinancialCategory.Taxes).Sum(x=>x.Amount);
        public decimal EstimatedTaxes=>FinanceSystem.Money(Math.Max(0,PretaxProfit-finance.State.AssessedTaxableProfit)*(decimal)finance.Settings.TaxRate);
        public decimal TaxDue=>finance.State.Obligations.Where(x=>!x.Paid && !x.Receivable && x.Category==FinancialCategory.Taxes).Sum(x=>x.Amount);
        public decimal TaxReserve=>Math.Min(Math.Max(0,finance.CashFlow.Cash),TaxDue+FinanceSystem.Money(Math.Max(0,PretaxProfit-finance.State.AssessedTaxableProfit)*(decimal)finance.Settings.TaxReserveFraction));
        public void OnTick(TickContext context)
        {
            finance.State.TaxReserve=TaxReserve;
            long cadence=(long)finance.Settings.TaxCadenceDays*finance.HoursPerDay;
            if(context.TotalHours-finance.State.LastTaxHour<cadence) return;
            finance.State.LastTaxHour=context.TotalHours;decimal tax=EstimatedTaxes;
            finance.State.AssessedTaxableProfit=Math.Max(finance.State.AssessedTaxableProfit,PretaxProfit);
            finance.AccrueExpense(null,tax,FinancialCategory.Taxes,"tax-"+context.TotalHours,"Periodic profit tax",checked(context.TotalHours+finance.HoursPerDay));
        }
    }
}
