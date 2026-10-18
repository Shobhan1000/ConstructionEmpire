using System;

namespace ConstructionEmpire.Data
{
    [Serializable]
    public class TransactionRecord
    {
        public string DateIso;
        public double Amount; // positive = income, negative = expense
        public string Reason;
    }
}
