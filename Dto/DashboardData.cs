using System;
namespace SmartInverterSimulator.Dto
{
    public class DashboardData
    {
        public int CustomerID { get; set; }

        public Decimal PowerGeneratedThisMonth { get; set; }

        public Decimal PowerConsumedThisMonth { get; set; }

        public Decimal PowerGeneratedToday { get; set; }

        public Decimal PowerConsumedToday { get; set; }

        public Decimal PowerGeneratedPerDay { get; set; }

        public Decimal PowerConsumedPerDay { get; set; }

        public Decimal BatteryPerc { get; set; }

        public Decimal CurrentLoadWatts { get; set; }

        public Decimal CurrentSolarOutputWatts { get; set; }

        public string PowerSource { get; set; }

        public DateTime LoggedAt { get; set; }


    }
}
