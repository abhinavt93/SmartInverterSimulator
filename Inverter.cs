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
        private int _gapBetweenTwoReadingsSec;
        private decimal _batteryStatusWh;
        private bool _isMinimumBatteryReached;

        private decimal _minimumBatteryWh;
        private decimal _sufficientBatteryWh;
        private decimal _fullBatteryWh;
        private decimal _batteryChargeEnergyWh;
        private decimal _solarGeneratedWh;
        private decimal _solarOutputWatts;
        private Config _config;

        public Inverter()
        {
            _rawData = new RawDataDto();
            _random = new Random();
            _config = Config.Instance();
            _gapBetweenTwoReadingsSec = _config.TimeGapSec;
            _batteryStatusWh = _config.InitialBatteryPerc * _config.BatteryCapacitykWh * 10;

            _minimumBatteryWh = _config.MinimumBatteryPerc * _config.BatteryCapacitykWh * 10;
            _sufficientBatteryWh = _minimumBatteryWh * 3M;
            _fullBatteryWh = _config.BatteryCapacitykWh * 1000;
            _batteryChargeEnergyWh = _config.BatteryMaximumChargeWatt * (_config.TimeGapSec / 3600M);

            if (_config.IsDataGenerationMode)
            {
                _currentTime = _config.DataGenerationStartDateTime;
                _gapBetweenTwoReadingsSec = 0;
            }

        }

        public async Task InitiateSimulatorAsync()
        {
            while (true)
            {
                while (ServerUpload.ConcurrentStack.Count > 500)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_config.TimeGapWhenQueueFullSec));
                    continue;
                }

                await refreshDataAsync();

                ServerUpload.ConcurrentStack.Push(_rawData);
                await Task.Delay(TimeSpan.FromSeconds(_gapBetweenTwoReadingsSec));
            }
        }

        private async Task refreshDataAsync()
        {
            refreshCurrentTime();

            _rawData.LoggedAt = _currentTime;
            _rawData.BatteryPerc = 50;
             
            _solarOutputWatts = getCurrentSolarOutput(_rawData.LoggedAt.Hour).Round(_config.RoundUpto);
            _solarGeneratedWh = calculateConsumptionOrGeneration(_solarOutputWatts);
            
            _rawData.LoadWatts = getCurrentLoad(_rawData.LoggedAt.Hour).Round(_config.RoundUpto);
            _rawData.ConsumptionWh = calculateConsumptionOrGeneration(_rawData.LoadWatts);
            _rawData.TimeIntervalSec = _config.TimeGapSec;
            _rawData.CustomerID = _config.CustomerID;
            await updateBatteryStatusAsync(_rawData.ConsumptionWh, _rawData.LoadWatts);

            _rawData.SolarOutputWatts = _solarOutputWatts;
            _rawData.SolarGeneratedWh = _solarGeneratedWh;
            _rawData.PowerSource = _config.PowerSource;
            _rawData.BatteryPerc = Math.Round((_batteryStatusWh / _fullBatteryWh * 100), 0);
        }

        private decimal calculateConsumptionOrGeneration(decimal input)
        {
            return input * (_config.TimeGapSec / 3600M);
        }

        private decimal getCurrentSolarOutput(int currentHour)
        {
            if (currentHour > 18 || currentHour < 6)
            {
                return 0;
            }
            else if ((currentHour >= 6 && currentHour < 11) || (currentHour >= 16 && currentHour <= 18))
            {
                return _random.NextDecimal(0.5M * _config.SolarPanelCapacityWatts, 0.7M * _config.SolarPanelCapacityWatts);
            }
            else
            {
                return _random.NextDecimal(0.75M * _config.SolarPanelCapacityWatts, 0.85M * _config.SolarPanelCapacityWatts);
            }
        }

        private decimal getCurrentLoad(int currentHour)
        {
            if (currentHour > 18 || currentHour < 6)
            {
                return _random.NextDecimal(0.9M * _config.MaximumLoadWatt, 0.95M * _config.MaximumLoadWatt);
            }
            else if ((currentHour >= 6 && currentHour < 11) || (currentHour >= 16 && currentHour <= 18))
            {
                return _random.NextDecimal(0.5M * _config.MaximumLoadWatt, 0.6M * _config.MaximumLoadWatt);
            }
            else
            {
                return _random.NextDecimal(0.1M * _config.MaximumLoadWatt, 0.2M * _config.MaximumLoadWatt);
            }
        }

        private void refreshCurrentTime()
        {
            if (_config.IsDataGenerationMode)
            {
                _currentTime = _currentTime.AddSeconds(_config.TimeGapSec);
            }
            else
            {
                _currentTime = DateTime.Now;
            }
        }

        //Assumption 1: Grid power always stays ON
        //Assumption 2: All solar power generated can be consumed by the battery
        private async Task updateBatteryStatusAsync(decimal consumptionWh, decimal loadWatts)
        {
            if (gridCutOffTimeReached())
            {
                while (_config.NextGridCutOffTime < _currentTime)
                {
                    _config.NextGridCutOffTime = _config.NextGridCutOffTime.AddDays(1);
                }
                _config.IsNextGridCutOffTimeUpdated = "N";
                await ServerUpload.UpdateNextGridCutOffTimeDBAsync(_config);

                _config.PowerSource = "B";
            }

            if (isSolarPowerGenerated())
            {
                await updateBatteryWhenSolarAsync(consumptionWh, loadWatts);
            }
            else
            {
                await updateBatteryWhenNoSolarAsync(consumptionWh);
            }
        }

        private async Task updateBatteryWhenSolarAsync(decimal consumptionWh, decimal loadWatts)
        {
            if (batteryFull())
            {
                if (_solarGeneratedWh >= consumptionWh)
                {
                    _solarGeneratedWh = consumptionWh;
                    _solarOutputWatts = loadWatts;
                    _config.PowerSource = "S";
                }
                else
                {
                    _batteryStatusWh -= consumptionWh - _solarGeneratedWh;
                    _config.PowerSource = "B+S";
                }

                _isMinimumBatteryReached = false;
                if (_config.IsNextGridCutOffTimeUpdated.Equals("N"))
                {
                    _config.NextGridCutOffTime = _config.NextGridCutOffTime.AddHours(-1);
                    _config.IsNextGridCutOffTimeUpdated = "Y";
                    await ServerUpload.UpdateNextGridCutOffTimeDBAsync(_config);
                }
                
            }
            else if (sufficientBattery())
            {
                _batteryStatusWh -= consumptionWh - _solarGeneratedWh;
                _config.PowerSource = "B+S";
                _isMinimumBatteryReached = false;
            }
            else if (lowBattery())
            {
                _batteryStatusWh += _batteryChargeEnergyWh;
                _config.PowerSource = "G+S";

                if (_solarGeneratedWh >= consumptionWh)
                {
                    _solarGeneratedWh = consumptionWh;
                    _solarOutputWatts = loadWatts;
                }

                _isMinimumBatteryReached = true;

                if (_config.IsNextGridCutOffTimeUpdated.Equals("N"))
                {
                    _config.NextGridCutOffTime = _config.NextGridCutOffTime.AddHours(1);
                    _config.IsNextGridCutOffTimeUpdated = "Y";
                    await ServerUpload.UpdateNextGridCutOffTimeDBAsync(_config);
                }
            }
        }

        private async Task updateBatteryWhenNoSolarAsync(decimal consumptionWh)
        {
            if (isPowerSourceBattery())
            {
                if (sufficientBattery())
                {
                    _batteryStatusWh -= consumptionWh;
                    _isMinimumBatteryReached = false;
                }
                else if (lowBattery())
                {
                    _batteryStatusWh += _batteryChargeEnergyWh;
                    _config.PowerSource = "G";
                    _isMinimumBatteryReached = true;

                    if (_config.IsNextGridCutOffTimeUpdated.Equals("N"))
                    {
                        _config.NextGridCutOffTime = _config.NextGridCutOffTime.AddHours(1);
                        _config.IsNextGridCutOffTimeUpdated = "Y";
                        await ServerUpload.UpdateNextGridCutOffTimeDBAsync(_config);
                    }
                }
            }
            else if (!batteryFull())
            {
                _batteryStatusWh += _config.BatteryMaximumChargeWatt * (_config.TimeGapSec / 3600M);
                _config.PowerSource = "G";
            }
        }

        private bool batteryFull() { return _batteryStatusWh >= _fullBatteryWh; }

        private bool isSolarPowerGenerated() { return _solarGeneratedWh > 0; }

        private bool isPowerSourceBattery() { return _config.PowerSource.Equals("B"); }

        private bool sufficientBattery() { return (_batteryStatusWh > _minimumBatteryWh && (!_isMinimumBatteryReached || _batteryStatusWh > _sufficientBatteryWh)); }

        private bool lowBattery() { return (_batteryStatusWh < _minimumBatteryWh || _isMinimumBatteryReached); }

        private bool gridCutOffTimeReached() { return _config.NextGridCutOffTime < _currentTime; }
    }
}
