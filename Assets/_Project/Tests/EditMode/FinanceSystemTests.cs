using System;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Finance;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class FinanceSystemTests
    {
        private SimulationManager sim; private FinanceSystem finance;
        [SetUp] public void Setup()
        {
            GameEvents.Clear(); sim=new SimulationManager(new CompanyData("Finance test",100),new GameTimeData(),42);
            finance=new FinanceSystem(sim.Company,sim.Time,new FinanceSettings {OfficeCostPerDay=0,InsurancePremium=0,TaxRate=0});sim.Register(finance);
        }
        [TearDown] public void Cleanup() { finance.Dispose();sim.Dispose();GameEvents.Clear(); }
        private ContractData Contract(decimal value=1000)
        {
            var project=new ProjectData {Id="p",ClientId="c",ContractValue=(float)value,Budget=800,DeadlineHour=10000};
            var contract=new ContractData {Id="contract",ProjectId="p",ClientId="c",ContractValue=value,CompletionDeadlineHour=10000,RetentionPercentage=.1m,WarrantyDurationHours=48};
            contract.PaymentSchedule.Add(new ContractPayment {Id="progress",Stage=PaymentStage.Progress,Fraction=.5m,ProgressThreshold=50,DelayHours=24});
            contract.PaymentSchedule.Add(new ContractPayment {Id="final",Stage=PaymentStage.Final,Fraction=.5m,ProgressThreshold=100});
            finance.RegisterContract(project,600,contract);finance.StartContract("p");return contract;
        }
        [Test] public void EarnedProfitCanBePositiveWhileCashIsNegativeUntilDelayedReceipt()
        {
            Contract();int warnings=0;GameEvents.Subscribe<CashFlowWarningEvent>(e=> {if(e.Critical) warnings++;});
            finance.UpdateProgress("p",60);finance.DelayNextPayment("p",2);
            finance.AccrueMaterialPurchase("p",400,"purchase",0);
            Assert.That(finance.Profit,Is.EqualTo(200));Assert.That(finance.CashFlow.Cash,Is.EqualTo(-300));
            Assert.That(sim.Company.CashBalance,Is.EqualTo(-300)); Assert.That(finance.AccountsReceivable,Is.EqualTo(450)); Assert.That(warnings,Is.EqualTo(1));
            for(int i=0;i<71;i++) sim.Advance(1);Assert.That(finance.CashFlow.Cash,Is.EqualTo(-300));
            sim.Advance(1);Assert.That(finance.CashFlow.Cash,Is.EqualTo(150));Assert.That(finance.Profit,Is.EqualTo(200));
        }
        [Test] public void RetentionIsWithheldUntilWarrantyEndsAndPaidExactlyOnce()
        {
            var c=Contract(1000.01m);finance.UpdateProgress("p",100);finance.CompleteContract("p",.8);
            Assert.That(c.RetentionAmount,Is.EqualTo(100m));
            for(int i=0;i<47;i++) sim.Advance(1);
            Assert.That(finance.CashFlow.Cash,Is.EqualTo(1000.01m));Assert.That(finance.AccountsReceivable,Is.EqualTo(100m));
            sim.Advance(1);Assert.That(finance.CashFlow.Cash,Is.EqualTo(1100.01m));
            finance.CompleteContract("p",.8);sim.Advance(1);Assert.That(finance.CashFlow.Cash,Is.EqualTo(1100.01m));
        }
        [Test] public void BonusDoesNotReduceFinalContractInvoice()
        {
            Contract();finance.RecordExtraRevenue("p",50,FinancialCategory.Bonus,"bonus",0);
            finance.CompleteContract("p",.8);for(int i=0;i<48;i++)sim.Advance(1);
            Assert.That(finance.CashFlow.Cash,Is.EqualTo(1150));Assert.That(finance.Revenue,Is.EqualTo(1050));
        }
        [Test] public void WorkAccruesWagesAndNonCashDepreciationWithoutImmediatePayroll()
        {
            Contract();finance.Settings.ProjectOverheadPerWorkHour=5;
            var machine=new EquipmentRuntimeData {Id="machine",PurchasePrice=10000};
            GameEvents.Publish(new ConstructionWorkAccruedEvent("p",100,10,20,2,new[]{machine}));
            Assert.That(finance.ProjectCosts("p"),Is.EqualTo(142));Assert.That(finance.AccountsPayable,Is.EqualTo(140));
            Assert.That(finance.CashFlow.Cash,Is.EqualTo(100));
            for(int i=0;i<168;i++)sim.Advance(1);Assert.That(finance.CashFlow.Cash,Is.EqualTo(-40));
            Assert.That(finance.AccountsPayable,Is.Zero);
        }
        [Test] public void PurchaseReferencePreventsDoubleCharge()
        { finance.AccrueMaterialPurchase("p",75,"purchase",0);finance.AccrueMaterialPurchase("p",75,"purchase",0);Assert.That(finance.Expenses,Is.EqualTo(75));Assert.That(finance.CashFlow.Cash,Is.EqualTo(25)); }
        [Test] public void LoanPrincipalIsNeitherRevenueNorExpenseButInterestIsExpense()
        {
            var loan=finance.Banking.DrawLoan(1200,12,"Fleet",10000);Assert.That(finance.CashFlow.Cash,Is.EqualTo(1300));Assert.That(finance.Revenue,Is.Zero);
            decimal interest=decimal.Round(1200*loan.AnnualInterestRate/12,2,MidpointRounding.AwayFromZero);
            for(int i=0;i<672;i++)sim.Advance(1);
            Assert.That(finance.Banking.Debt,Is.EqualTo(1100));Assert.That(finance.Expenses,Is.EqualTo(interest));Assert.That(finance.CashFlow.Cash,Is.EqualTo(1200-interest));
            Assert.Throws<InvalidOperationException>(()=>finance.Banking.DrawLoan(100,12,"Fleet",0));
        }
        [Test] public void InsuranceAndAbstractTaxUseRecurringCadenceWithoutDuplicateAssessment()
        {
            finance.Settings.InsurancePremium=10;finance.Settings.InsuranceCadenceDays=1;finance.Settings.TaxCadenceDays=1;finance.Settings.TaxRate=.2;
            finance.RecordExtraRevenue("p",1000,FinancialCategory.Bonus,"bonus",0);
            for(int i=0;i<24;i++)sim.Advance(1);
            Assert.That(finance.Expenses,Is.EqualTo(208));Assert.That(finance.Taxes.TaxDue,Is.EqualTo(198));
            finance.OnTick(new TickContext(24,24,7,28));Assert.That(finance.Expenses,Is.EqualTo(208));
            for(int i=0;i<24;i++)sim.Advance(1);Assert.That(finance.Expenses,Is.EqualTo(218));Assert.That(finance.Taxes.TaxDue,Is.Zero);
        }
        [Test] public void CompletionReputationCombinesProfitScheduleAndQualityOnlyOnce()
        {
            var c=Contract(); finance.CompleteContract("p",.8);Assert.That(sim.Company.Reputation,Is.EqualTo(55));
            finance.CompleteContract("p",.8);Assert.That(sim.Company.Reputation,Is.EqualTo(55));Assert.That(c.ReputationApplied,Is.True);
        }
    }
}
