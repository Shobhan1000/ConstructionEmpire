using System;
using System.Collections.Generic;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Finance
{
    /// <summary>
    /// Lightweight transaction ledger (slice of spec sections 32/38).
    /// Auto-logs every cash movement by subscribing to
    /// CompanyCashChangedEvent, so no other system has to remember to
    /// report to it - CompanySystem stays the single source of truth for
    /// cash, this just keeps a readable history of how it moved.
    /// </summary>
    public class FinanceSystem
    {
        private const int MaxRetainedTransactions = 500;
        private readonly GameTimeManager _timeManager;

        public List<TransactionRecord> Transactions { get; private set; } = new List<TransactionRecord>();

        public FinanceSystem(GameTimeManager timeManager)
        {
            _timeManager = timeManager;
            EventBus.Subscribe<CompanyCashChangedEvent>(OnCashChanged);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<CompanyCashChangedEvent>(OnCashChanged);
        }

        private void OnCashChanged(CompanyCashChangedEvent evt)
        {
            DateTime date = _timeManager != null ? _timeManager.CurrentDate : DateTime.UtcNow;

            Transactions.Add(new TransactionRecord
            {
                DateIso = date.ToString("o"),
                Amount = evt.Delta,
                Reason = evt.Reason
            });

            if (Transactions.Count > MaxRetainedTransactions)
            {
                Transactions.RemoveRange(0, Transactions.Count - MaxRetainedTransactions);
            }
        }

        public double NetTotal()
        {
            double sum = 0;
            foreach (var t in Transactions)
            {
                sum += t.Amount;
            }
            return sum;
        }

        public List<TransactionRecord> CaptureState() => Transactions;

        public void RestoreState(List<TransactionRecord> state)
        {
            Transactions = state ?? new List<TransactionRecord>();
        }
    }
}
