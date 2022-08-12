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
                Config.Instance().CustomerID = 610;
                var userData = await ServerUpload.GetUserDataAndConfig(Config.Instance().CustomerID);
                if (userData.IsFirstRun == "Y")
                {
                    Config.Instance().InitialBatteryPerc = 100;
                    Config.Instance().PowerSource = "G";
                    Config.Instance().IsFirstRun = "N";
                    await ServerUpload.UpdateIsFirstRunDB(Config.Instance());
                    
                }
                else
                {
                    var dashboardData = await ServerUpload.GetDashboardData(Config.Instance().CustomerID);
                    Config.Instance().InitialBatteryPerc = dashboardData.BatteryPerc;
                    Config.Instance().PowerSource = dashboardData.PowerSource;
                }
                
                Config.Instance().SolarPanelCapacityWatts = userData.SolarPanelCapacityWatts;
                Config.Instance().BatteryCapacitykWh = userData.BatteryCapacitykWh;
                Config.Instance().MinimumBatteryPerc = userData.MinimumBatteryPerc;
                Config.Instance().NextGridCutOffTime = userData.NextGridCutOffTime;
                Config.Instance().MaximumLoadWatt = 320;
                Config.Instance().TimeGapSec = 2;
                Config.Instance().TimeGapWhenQueueFullSec = 10;
                Config.Instance().RoundUpto = 2;
                Config.Instance().BatteryMaximumChargeWatt = 120;
                

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
