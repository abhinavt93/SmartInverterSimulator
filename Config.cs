using System;
namespace SmartInverterSimulator
{
    public static class Config
    {
        public static int MaximumSolarPanelCapacityWatt { get; set; }

        public static int MaximumLoadWatt { get; set; }

        public static int TimeGapSec { get; set; }

        public static int TimeGapWhenQueueFullSec { get; set; }

        public static int RoundUpto { get; set; }
    }
}
