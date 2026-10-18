using System;
namespace ConstructionEmpire.Data.Bidding
{
    public static class BidValidation
    {
        public static void Text(string value, string name)
        { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", name); }
        public static void NonNegative(double value, string name)
        { if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(name); }
        public static void Positive(double value, string name)
        { NonNegative(value, name); if (value == 0) throw new ArgumentOutOfRangeException(name); }
        public static void Unit(double value, string name)
        { NonNegative(value, name); if (value > 1) throw new ArgumentOutOfRangeException(name); }
        public static float Money(double value)
        { NonNegative(value, nameof(value)); if (value > float.MaxValue) throw new OverflowException("Money exceeds supported range."); return (float)value; }
    }
}
