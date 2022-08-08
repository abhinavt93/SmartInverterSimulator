using System;
namespace SmartInverterSimulator.Extensions
{
    public static class DecimalExtension
    {
        public static Decimal Round(this Decimal value, int upto)
        {
            return Math.Round(value, upto);
        }
    }
}
