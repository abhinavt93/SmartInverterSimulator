using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using SmartInverterSimulator.Dto;

namespace SmartInverterSimulator
{
    public class ServerUpload
    {
        public static ConcurrentStack<RawDataDto> ConcurrentStack = new ConcurrentStack<RawDataDto>();
        private Random _random = new Random();
        //public static Channel<RawDataDto> Channel;

        public ServerUpload()
        {
            
        }

        public async Task ProcessQueue()
        {
            while (true)
            {
                if (ConcurrentStack.TryPop(out RawDataDto rawData))
                {
                    await printToConsole(rawData);    
                }
                else
                {
                    await Task.Delay(Config.TimeGapSec);
                }
                
            }
        }

        private async Task printToConsole(RawDataDto rawData)
        {
            bool retry;
            do
            {
                try
                {
                    Console.WriteLine();
                    Console.WriteLine($"rawData.LoggedAt: {rawData.LoggedAt} ");
                    Console.WriteLine($"rawData.CustomerID: {rawData.CustomerID} ");
                    //Console.WriteLine($"Random: {20 / _random.Next(-1, 4)}");
                    Console.WriteLine($"rawData.BatteryPerc: {rawData.BatteryPerc}");
                    Console.WriteLine($"rawData.SolarOutputWatts: {rawData.SolarOutputWatts}");
                    Console.WriteLine($"rawData.SessionSolarGeneratedWatts: {rawData.SolarGeneratedWh}");
                    Console.WriteLine($"rawData.LoadWatts: {rawData.LoadWatts}");
                    Console.WriteLine($"rawData.SessionConsumptionWatts : {rawData.ConsumptionWh}");
                    Console.WriteLine($"rawData.TimeIntervalSec : {rawData.TimeIntervalSec}");

                    await pushToServer(rawData);
                    retry = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" Exception: {ex.Message}");
                    retry = true;
                    await Task.Delay(5000);
                }

            }
            while (retry);

        }

        private async Task pushToServer(RawDataDto rawData)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.PostAsJsonAsync("https://localhost:5001/RawDataAPI/ProcessRawData", rawData);
            response.EnsureSuccessStatusCode();
        }

    }
}
