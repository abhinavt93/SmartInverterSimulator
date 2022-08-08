using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SmartInverterSimulator
{
    class Program
    {
        
        static void Main(string[] args)
        {
            Config.MaximumSolarPanelCapacityWatt = 320;
            Config.MaximumLoadWatt = 320;
            Config.TimeGapSec = 2;
            Config.TimeGapWhenQueueFullSec = 10;
            Config.RoundUpto = 2;

            //ServerUpload serverUpload = new ServerUpload();
            Task taskInverter = new Inverter().InitiateSimulatorAsync();
            Task taskserverUpload = new ServerUpload().ProcessQueue();
            //Console.WriteLine($"Current hour: {DateTime.Now.Hour}");
            Task.WaitAll(taskInverter, taskserverUpload);
        }
        
    }
}
