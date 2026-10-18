using System;

namespace ConstructionEmpire.Data
{
    [Serializable]
    public class MaterialStockEntry
    {
        public MaterialType Type;
        public double QuantityOnHand;
        public double QuantityOnOrder;
    }
}
