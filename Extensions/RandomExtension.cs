using System;
namespace SmartInverterSimulator.Extensions
{
    public static class RandomExtension
    {
        public static Decimal NextDecimal(this Random random, Decimal minimum, Decimal maximum)
        {
            return (Decimal)random.NextDouble() * (maximum - minimum) + minimum;
        }
    }
}
