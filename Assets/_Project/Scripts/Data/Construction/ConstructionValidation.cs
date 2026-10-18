using System;
namespace ConstructionEmpire.Data.Construction
{
    public static class ConstructionValidation
    {
        public static void Text(string value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required."); }
        public static void NonNegative(double value, string name) { if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(name); }
        public static void Positive(double value, string name) { NonNegative(value,name); if (value == 0) throw new ArgumentOutOfRangeException(name); }
        public static void Unit(double value, string name) { NonNegative(value,name); if (value > 1) throw new ArgumentOutOfRangeException(name); }
        public static void Percent(double value, string name) { NonNegative(value,name); if (value > 100) throw new ArgumentOutOfRangeException(name); }
    }
}
