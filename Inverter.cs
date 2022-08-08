using System;
using System.Threading;
using System.Threading.Tasks;
using SmartInverterSimulator.Extensions;
using SmartInverterSimulator.Dto;

namespace SmartInverterSimulator
{
    public class Inverter
    {
        private RawDataDto _rawData;
        private Random _random;
        private DateTime _currentTime;
        private bool _isDataGenerationMode = false;
        private int _gapBetweenTwoReadingsSec;

        public Inverter()
        {
            _rawData = new RawDataDto();
            _random = new Random();
            _gapBetweenTwoReadingsSec = Config.TimeGapSec;

            if (_isDataGenerationMode)
            {
                _currentTime = new DateTime(year: 2022, month: 1, day: 1);
                _gapBetweenTwoReadingsSec = 0;
            }

        }

        public async Task InitiateSimulatorAsync()
        {
            while (true)
            {
                while (ServerUpload.ConcurrentStack.Count > 500)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Config.TimeGapWhenQueueFullSec));
                    continue;
                }

                refreshData();

                ServerUpload.ConcurrentStack.Push(_rawData);
                await Task.Delay(TimeSpan.FromSeconds(_gapBetweenTwoReadingsSec));
            }
        }

        private void refreshData()
        {
            refreshCurrentTime();

            _rawData.LoggedAt = _currentTime;
            _rawData.BatteryPerc = 50;
            _rawData.SolarOutputWatts = getCurrentSolarOutput(_rawData.LoggedAt.Hour).Round(Config.RoundUpto);
            _rawData.SolarGeneratedWh = calculateConsumptionOrGeneration(_rawData.SolarGeneratedWh, _rawData.SolarOutputWatts);
            _rawData.LoadWatts = getCurrentLoad(_rawData.LoggedAt.Hour).Round(Config.RoundUpto);
            _rawData.ConsumptionWh = calculateConsumptionOrGeneration(_rawData.ConsumptionWh, _rawData.LoadWatts);
            _rawData.TimeIntervalSec = Config.TimeGapSec;
            _rawData.PowerSource = "S";
            _rawData.CustomerID = 610;
        }

        private Decimal calculateConsumptionOrGeneration(Decimal currentConsumption, Decimal currentLoad)
        {
            return currentLoad * (Config.TimeGapSec / 3600M);
        }

        private Decimal getCurrentSolarOutput(int currentHour)
        {
            if (currentHour > 18 || currentHour < 6)
            {
                return 0;
            }
            else if ((currentHour >= 6 && currentHour < 11) || (currentHour >= 16 && currentHour <= 18))
            {
                return _random.NextDecimal(0.5M * Config.MaximumSolarPanelCapacityWatt, 0.7M * Config.MaximumSolarPanelCapacityWatt);
            }
            else
            {
                return _random.NextDecimal(0.75M * Config.MaximumSolarPanelCapacityWatt, 0.85M * Config.MaximumSolarPanelCapacityWatt);
            }
        }

        private Decimal getCurrentLoad(int currentHour)
        {
            if (currentHour > 18 || currentHour < 6)
            {
                return _random.NextDecimal(0.9M * Config.MaximumLoadWatt, 0.95M * Config.MaximumLoadWatt);
            }
            else if ((currentHour >= 6 && currentHour < 11) || (currentHour >= 16 && currentHour <= 18))
            {
                return _random.NextDecimal(0.5M * Config.MaximumLoadWatt, 0.6M * Config.MaximumLoadWatt);
            }
            else
            {
                return _random.NextDecimal(0.1M * Config.MaximumLoadWatt, 0.2M * Config.MaximumLoadWatt);
            }
        }

        private void refreshCurrentTime()
        {
            if (_isDataGenerationMode)
            {
                _currentTime = _currentTime.AddSeconds(Config.TimeGapSec);
            }
            else
            {
                _currentTime = DateTime.Now;
            }
        }
    }
}
