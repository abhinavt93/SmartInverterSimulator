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

        public decimal MinimumBatteryPerc { get; set; }

        public decimal BatteryCapacitykWh { get; set; }

        public decimal SolarPanelCapacityWatts { get; set; }

        public int MaximumLoadWatt { get; set; }

        public int TimeGapSec { get; set; }

        public int TimeGapWhenQueueFullSec { get; set; }

        public int RoundUpto { get; set; }

        public decimal InitialBatteryPerc { get; set; }

        public decimal BatteryMaximumChargeWatt { get; set; }

        public DateTime NextGridCutOffTime { get; set; }

        public string PowerSource { get; set; }

        public string IsFirstRun { get; set; }

        public string IsNextGridCutOffTimeUpdated { get; set; }

        public bool IsDataGenerationMode { get; set; }

        public DateTime DataGenerationStartDateTime { get; set; }

        public DateTime LoggedAt { get; set; }
    }
}
