using System;
namespace SmartInverterSimulator
{
    public class Config
    {
        private Config()
        {

        }

        private static Config config;


        public static Config Instance()
        {
            if(config == null)
            {
                config = new Config();
            }

            return config;
        }

        public int CustomerID { get; set; }

        public Decimal MinimumBatteryPerc { get; set; }

        public Decimal BatteryCapacitykWh { get; set; }

        public Decimal SolarPanelCapacityWatts { get; set; }

        public int MaximumLoadWatt { get; set; }

        public int TimeGapSec { get; set; }

        public int TimeGapWhenQueueFullSec { get; set; }

        public int RoundUpto { get; set; }

        public Decimal InitialBatteryPerc { get; set; }

        public Decimal BatteryMaximumChargeWatt { get; set; }

        public DateTime NextGridCutOffTime { get; set; }

        public DateTime LoggedAt { get; set; }
    }
}
