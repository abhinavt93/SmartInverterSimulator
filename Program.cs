using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SmartInverterSimulator
{
    class Program
    {
        
        static async Task Main(string[] args)
        {
            try
            {
                var result = await ServerUpload.GetUserDataAndConfig(customerID: 610);
                Config.Instance().CustomerID = 610;
                Config.Instance().SolarPanelCapacityWatts = result.SolarPanelCapacityWatts;
                Config.Instance().BatteryCapacitykWh = result.BatteryCapacitykWh;
                Config.Instance().MinimumBatteryPerc = result.MinimumBatteryPerc;
                Config.Instance().NextGridCutOffTime = result.NextGridCutOffTime;
                Config.Instance().MaximumLoadWatt = 320;
                Config.Instance().TimeGapSec = 2;
                Config.Instance().TimeGapWhenQueueFullSec = 10;
                Config.Instance().RoundUpto = 2;
                Config.Instance().BatteryMaximumChargeWatt = 120;
                Config.Instance().InitialBatteryPerc = await ServerUpload.GetBatteryStatus(customerID: 610);

                Task taskInverter = new Inverter().InitiateSimulatorAsync();
                Task taskserverUpload = new ServerUpload().ProcessQueue();

                Task.WaitAll(taskInverter, taskserverUpload);
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }
        
    }
}
