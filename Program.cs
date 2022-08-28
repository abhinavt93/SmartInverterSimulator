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
                var userData = await ServerUpload.GetUserDataAndConfigAsync(Config.Instance().CustomerID);
                var dashboardData = await ServerUpload.GetDashboardDataAsync(Config.Instance().CustomerID);

                if (userData.IsFirstRun == "Y")
                {
                    Config.Instance().InitialBatteryPerc = 100;
                    Config.Instance().PowerSource = "G";
                    Config.Instance().IsFirstRun = "N";
                    await ServerUpload.UpdateIsFirstRunDBAsync(Config.Instance());
                    
                }
                else
                {
                    Config.Instance().InitialBatteryPerc = dashboardData.BatteryPerc;
                    Config.Instance().PowerSource = dashboardData.PowerSource;
                }

                Config.Instance().IsNextGridCutOffTimeUpdated = userData.IsNextGridCutOffTimeUpdated;
                Config.Instance().SolarPanelCapacityWatts = userData.SolarPanelCapacityWatts;
                Config.Instance().BatteryCapacitykWh = userData.BatteryCapacitykWh;
                Config.Instance().MinimumBatteryPerc = userData.MinimumBatteryPerc;
                Config.Instance().NextGridCutOffTime = userData.NextGridCutOffTime;
                Config.Instance().MaximumLoadWatt = 500;
                Config.Instance().TimeGapSec = 2;
                Config.Instance().TimeGapWhenQueueFullSec = 10;
                Config.Instance().RoundUpto = 2;
                Config.Instance().BatteryMaximumChargeWatt = 120;
                Config.Instance().IsDataGenerationMode = true;
                Config.Instance().DataGenerationStartDateTime = dashboardData.LoggedAt.AddMinutes(5);

                Task taskInverter = new Inverter().InitiateSimulatorAsync();
                Task taskserverUpload = new ServerUpload().ProcessQueueAsync();

                Task.WaitAll(taskInverter, taskserverUpload);
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }
    }
}
