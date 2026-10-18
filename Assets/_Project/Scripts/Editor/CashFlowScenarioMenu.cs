using System;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class CashFlowScenarioMenu
    {
        [MenuItem("Tools/Debug/Simulate Cash Flow Scenario")]
        public static void Run()
        {
            if(!Application.isPlaying) { Debug.LogWarning("Cash flow scenario requires Play Mode. No changes made."); return; }
            var bootstrap=UnityEngine.Object.FindFirstObjectByType<SimulationBootstrapper>();
            var finance=bootstrap?.Management?.Finance;
            if(finance==null) { Debug.LogWarning("No active FinanceSystem/CashFlowTracker. Create the simulation bootstrap first."); return; }
            if(finance.GetContract("debug-cash-flow")!=null) { Debug.Log("Cash flow scenario already applied in this session."); return; }
            bootstrap.Management.Commands.MarkDiagnosticMutation("forced cash-flow scenario");
            var settings=finance.Settings;
            decimal cost=Math.Max((decimal)settings.DebugCost,finance.CashFlow.Cash+(decimal)settings.LowCashThreshold+1);
            decimal value=Math.Max((decimal)settings.DebugContractValue,cost*2);
            var project=new ProjectData {Id="debug-cash-flow",ClientId="debug-client",ContractValue=(float)value,Budget=(float)cost,DeadlineHour=finance.Hour+10000};
            var contract=new ContractData {Id="debug-contract",ProjectId=project.Id,ClientId=project.ClientId,ContractValue=value,StartHour=finance.Hour,CompletionDeadlineHour=project.DeadlineHour,RetentionPercentage=.05m,WarrantyDurationHours=(long)settings.WarrantyDays*finance.HoursPerDay};
            contract.PaymentSchedule.Add(new ContractPayment {Id="progress",Stage=PaymentStage.Progress,Fraction=.5m,ProgressThreshold=50,DelayHours=1});
            contract.PaymentSchedule.Add(new ContractPayment {Id="final",Stage=PaymentStage.Final,Fraction=.5m,ProgressThreshold=100});
            finance.RegisterContract(project,cost,contract); finance.StartContract(project.Id);
            finance.UpdateProgress(project.Id,Math.Max(60,settings.DebugProgress)); finance.DelayNextPayment(project.Id,settings.DebugPaymentDelayDays);
            finance.AccrueExpense(project.Id,cost,FinancialCategory.Materials,"debug-front-cost","Debug: front-loaded project purchases",finance.Hour);
            GameEvents.Publish(new NavigationRequestedEvent("Finance"));
            Debug.Log($"Cash flow scenario applied: earned profit {finance.Revenue-finance.ProjectCosts(project.Id):0.00}, cash {finance.CashFlow.Cash:0.00}; progress receipt delayed by {settings.DebugPaymentDelayDays} days. The cash deficit warning should be visible.");
        }
    }
}
