using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Finance
{
    public enum LedgerKind { Revenue, Expense, CashReceipt, CashPayment, LoanDraw, PrincipalRepayment }
    public enum FinancialCategory { Contract, ProgressPayment, FinalPayment, Retention, ChangeOrder, Bonus, Labor, Materials, Equipment, Maintenance, Rental, Subcontractors, Office, Insurance, Transportation, Financing, Legal, Taxes, Penalty, Other }
    [Serializable,DataContract]
    public sealed class LedgerEntry
    {
        [DataMember] public long Sequence,Hour;
        [DataMember] public string ProjectId,Reference,Description;
        [DataMember] public LedgerKind Kind;
        [DataMember] public FinancialCategory Category;
        [DataMember] public decimal Amount;
    }
    [Serializable,DataContract]
    public sealed class FinancialObligation
    {
        [DataMember] public string Id,ProjectId,Description;
        [DataMember] public decimal Amount;
        [DataMember] public long DueHour;
        [DataMember] public FinancialCategory Category;
        [DataMember] public bool Receivable,Paid;
    }
    [Serializable,DataContract]
    public sealed class LoanData
    {
        [DataMember] public string Id,Collateral;
        [DataMember] public decimal Principal,OutstandingPrincipal,AnnualInterestRate,MonthlyPrincipal;
        [DataMember] public int TermMonths,PaymentsMade;
        [DataMember] public long NextPaymentHour;
    }
    [Serializable,DataContract]
    public sealed class FinanceSaveData : SaveDataBase
    {
        [DataMember] public List<int> InsurancePolicies=new List<int>{0};
        [DataMember] public List<string> InsuranceClaims=new List<string>();
        [DataMember] public decimal OpeningCash,Cash,TaxReserve,AssessedTaxableProfit;
        [DataMember] public long LastTickHour,LastInsuranceHour,LastTaxHour,Sequence;
        [DataMember] public int CreditScore;
        [DataMember] public List<ContractData> Contracts=new List<ContractData>();
        [DataMember] public List<LedgerEntry> Ledger=new List<LedgerEntry>();
        [DataMember] public List<FinancialObligation> Obligations=new List<FinancialObligation>();
        [DataMember] public List<LoanData> Loans=new List<LoanData>();
        public override void Migrate() { if(SchemaVersion!=1) { base.Migrate(); return; } SchemaVersion=2; }
    }
}
