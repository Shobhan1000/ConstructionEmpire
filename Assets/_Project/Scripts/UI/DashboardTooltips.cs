using System.Collections.Generic;
namespace ConstructionEmpire.UI
{
    internal static class DashboardTooltips
    {
        private static readonly Dictionary<string,string> Text=new Dictionary<string,string>
        {
            {"cash","Cash is money available now, so a profitable project can still leave you unable to pay today’s bills."},
            {"revenue","Revenue is the value of work earned, which may be recorded before the client pays you."},
            {"expenses","Expenses include costs already incurred even when their bills have not yet been paid."},
            {"profit","Profit is earned revenue minus accrued costs and does not tell you how much money is in the bank."},
            {"reputation","Reputation reflects your track record and affects how confidently clients consider your bids."},
            {"active-count","Active projects have started construction and compete for your available people, equipment and cash."},
            {"employee-count","Employee count includes your regional workforce, including people who are assigned or temporarily absent."},
            {"equipment-count","Equipment count includes your fleet even when a machine is assigned, broken or out of fuel."},
            {"cash-flow","Cash flow tracks money actually received and paid, so delayed receipts can create a deficit despite positive profit."},
            {"retention","Retention is part of the contract price held back until the warranty release condition is met."},
            {"critical-path","A critical-path phase has no spare schedule time, so delaying it pushes back the predicted project finish."},
            {"risk","Risk combines site, schedule, supply, design, weather and crew factors to estimate exposure to disruption."},
            {"forecast","Forecast cost estimates the final bill from current progress and spending, rather than showing only costs paid so far."},
            {"expected-profit","Forecast profit is the contract value minus estimated final costs and can change as work unfolds."},
            {"completion","Progress measures completed work across phases, weighted by their planned duration."},
            {"remaining","Time remaining compares the current game time with the contract deadline, not the forecast finish."},
            {"quality","Quality describes the current construction outcome and can affect inspection results and final reputation."},
            {"safety","Crew safety shows the assigned workers’ safety capability and does not guarantee an incident-free site."},
            {"schedule","Schedule compares the planned and predicted finish, including delays on the current critical path."},
            {"expected-date","Expected completion is a forecast from current conditions, not a promised delivery date."},
            {"current-cost","Incurred cost includes work and purchases already charged to the project whether or not cash has moved."},
            {"budget","Budget is the project spending allowance used to flag overruns, rather than cash reserved in a separate bank account."},
            {"contract","Contract value is the agreed project price before any applicable penalties or later changes."},
            {"accounts-receivable","Receivables are amounts owed to your company that have not yet arrived as cash."},
            {"accounts-payable","Payables are bills your company owes that have not yet been paid."},
            {"debt","Loan principal increases cash when borrowed but must be repaid with interest and is not project revenue."},
            {"estimate-contingency","Contingency adds an allowance for uncertainty to your bid without guaranteeing that actual costs will stay within it."},
            {"equipment-condition","Low equipment condition increases breakdown exposure, which can stop a phase even when its crew is ready."}
        };
        public static string For(string key)
        {if(key==null)return null;if(key.StartsWith("detail-"))key=key.Substring(7);return Text.TryGetValue(key,out var text)?text:null;}
    }
}
