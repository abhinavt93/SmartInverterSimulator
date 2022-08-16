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
        private bool _isMinimumBatteryReached = false;

        private decimal _minimumBatteryWh;
        private decimal _sufficientBatteryWh;
        private decimal _fullBatteryWh;
        private decimal _batteryChargeEnergyWh;
        private decimal _solarGeneratedWh;
        private decimal _solarOutputWatts;

        public Inverter()
        {
            _rawData = new RawDataDto();
            _random = new Random();
            _gapBetweenTwoReadingsSec = Config.Instance().TimeGapSec;
            _batteryStatusWh = Config.Instance().InitialBatteryPerc * Config.Instance().BatteryCapacitykWh * 10;

            _minimumBatteryWh = Config.Instance().MinimumBatteryPerc * Config.Instance().BatteryCapacitykWh * 10;
            _sufficientBatteryWh = _minimumBatteryWh * 3M;
            _fullBatteryWh = Config.Instance().BatteryCapacitykWh * 1000;
            _batteryChargeEnergyWh = Config.Instance().BatteryMaximumChargeWatt * (Config.Instance().TimeGapSec / 3600M);

            if (Config.Instance().IsDataGenerationMode)
            {
                _currentTime = Config.Instance().DataGenerationStartDateTime;
                _gapBetweenTwoReadingsSec = 0;
            }

        }

        public async Task InitiateSimulatorAsync()
        {
            while (true)
            {
                while (ServerUpload.ConcurrentStack.Count > 500)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Config.Instance().TimeGapWhenQueueFullSec));
                    continue;
                }

                await refreshData();

                ServerUpload.ConcurrentStack.Push(_rawData);
                await Task.Delay(TimeSpan.FromSeconds(_gapBetweenTwoReadingsSec));
            }
        }

        private async Task refreshData()
        {
            refreshCurrentTime();

            _rawData.LoggedAt = _currentTime;
            _rawData.BatteryPerc = 50;
             
            _solarOutputWatts = getCurrentSolarOutput(_rawData.LoggedAt.Hour).Round(Config.Instance().RoundUpto);
            _solarGeneratedWh = calculateConsumptionOrGeneration(_solarOutputWatts);
            
            _rawData.LoadWatts = getCurrentLoad(_rawData.LoggedAt.Hour).Round(Config.Instance().RoundUpto);
            _rawData.ConsumptionWh = calculateConsumptionOrGeneration(_rawData.LoadWatts);
            _rawData.TimeIntervalSec = Config.Instance().TimeGapSec;
            _rawData.CustomerID = Config.Instance().CustomerID;
            await updateBatteryStatus(_rawData.ConsumptionWh, _rawData.LoadWatts);

            _rawData.SolarOutputWatts = _solarOutputWatts;
            _rawData.SolarGeneratedWh = _solarGeneratedWh;
            _rawData.PowerSource = Config.Instance().PowerSource;
            _rawData.BatteryPerc = Math.Round((_batteryStatusWh / _fullBatteryWh * 100), 0);
        }

        private decimal calculateConsumptionOrGeneration(decimal input)
        {
            return input * (Config.Instance().TimeGapSec / 3600M);
        }

        private decimal getCurrentSolarOutput(int currentHour)
        {
            if (currentHour > 18 || currentHour < 6)
            {
                return 0;
            }
            else if ((currentHour >= 6 && currentHour < 11) || (currentHour >= 16 && currentHour <= 18))
            {
                return _random.NextDecimal(0.5M * Config.Instance().SolarPanelCapacityWatts, 0.7M * Config.Instance().SolarPanelCapacityWatts);
            }
            else
            {
                return _random.NextDecimal(0.75M * Config.Instance().SolarPanelCapacityWatts, 0.85M * Config.Instance().SolarPanelCapacityWatts);
            }
        }

        private decimal getCurrentLoad(int currentHour)
        {
            if (currentHour > 18 || currentHour < 6)
            {
                return _random.NextDecimal(0.9M * Config.Instance().MaximumLoadWatt, 0.95M * Config.Instance().MaximumLoadWatt);
            }
            else if ((currentHour >= 6 && currentHour < 11) || (currentHour >= 16 && currentHour <= 18))
            {
                return _random.NextDecimal(0.5M * Config.Instance().MaximumLoadWatt, 0.6M * Config.Instance().MaximumLoadWatt);
            }
            else
            {
                return _random.NextDecimal(0.1M * Config.Instance().MaximumLoadWatt, 0.2M * Config.Instance().MaximumLoadWatt);
            }
        }

        private void refreshCurrentTime()
        {
            if (Config.Instance().IsDataGenerationMode)
            {
                _currentTime = _currentTime.AddSeconds(Config.Instance().TimeGapSec);
            }
            else
            {
                _currentTime = DateTime.Now;
            }
        }

        //Assumption 1: Grid power always stays ON
        //Assumption 2: All solar power generated can be consumed by the battery
        private async Task updateBatteryStatus(decimal consumptionWh, decimal loadWatts)
        {
            if (gridCutOffTimeReached())
            {
                while (Config.Instance().NextGridCutOffTime < _currentTime)
                {
                    Config.Instance().NextGridCutOffTime = Config.Instance().NextGridCutOffTime.AddDays(1);
                }
                Config.Instance().IsNextGridCutOffTimeUpdated = "N";
                await ServerUpload.UpdateNextGridCutOffTimeDB(Config.Instance());

                Config.Instance().PowerSource = "B";
            }

            if (isSolarPowerGenerated())
            {
                await updateBatteryWhenSolar(consumptionWh, loadWatts);
            }
            else
            {
                await updateBatteryWhenNoSolar(consumptionWh);
            }
        }

        private async Task updateBatteryWhenSolar(decimal consumptionWh, decimal loadWatts)
        {
            if (batteryFull())
            {
                if (_solarGeneratedWh >= consumptionWh)
                {
                    _solarGeneratedWh = consumptionWh;
                    _solarOutputWatts = loadWatts;
                    Config.Instance().PowerSource = "S";
                }
                else
                {
                    _batteryStatusWh -= consumptionWh - _solarGeneratedWh;
                    Config.Instance().PowerSource = "B+S";
                }

                _isMinimumBatteryReached = false;
                if (Config.Instance().IsNextGridCutOffTimeUpdated.Equals("N"))
                {
                    Config.Instance().NextGridCutOffTime = Config.Instance().NextGridCutOffTime.AddHours(-1);
                    Config.Instance().IsNextGridCutOffTimeUpdated = "Y";
                    await ServerUpload.UpdateNextGridCutOffTimeDB(Config.Instance());
                }
                
            }
            else if (sufficientBattery())
            {
                _batteryStatusWh -= consumptionWh - _solarGeneratedWh;
                Config.Instance().PowerSource = "B+S";
                _isMinimumBatteryReached = false;
            }
            else if (lowBattery())
            {
                _batteryStatusWh += _batteryChargeEnergyWh;
                Config.Instance().PowerSource = "G+S";

                if (_solarGeneratedWh >= consumptionWh)
                {
                    _solarGeneratedWh = consumptionWh;
                    _solarOutputWatts = loadWatts;
                }

                _isMinimumBatteryReached = true;

                if (Config.Instance().IsNextGridCutOffTimeUpdated.Equals("N"))
                {
                    Config.Instance().NextGridCutOffTime = Config.Instance().NextGridCutOffTime.AddHours(1);
                    Config.Instance().IsNextGridCutOffTimeUpdated = "Y";
                    await ServerUpload.UpdateNextGridCutOffTimeDB(Config.Instance());
                }
            }
        }

        private async Task updateBatteryWhenNoSolar(decimal consumptionWh)
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
                    Config.Instance().PowerSource = "G";
                    _isMinimumBatteryReached = true;

                    if (Config.Instance().IsNextGridCutOffTimeUpdated.Equals("N"))
                    {
                        Config.Instance().NextGridCutOffTime = Config.Instance().NextGridCutOffTime.AddHours(1);
                        Config.Instance().IsNextGridCutOffTimeUpdated = "Y";
                        await ServerUpload.UpdateNextGridCutOffTimeDB(Config.Instance());
                    }
                }
            }
            else if (!batteryFull())
            {
                _batteryStatusWh += Config.Instance().BatteryMaximumChargeWatt * (Config.Instance().TimeGapSec / 3600M);
                Config.Instance().PowerSource = "G";
            }
        }

        private bool batteryFull() { return _batteryStatusWh >= _fullBatteryWh; }

        private bool isSolarPowerGenerated() { return _solarGeneratedWh > 0; }

        private bool isPowerSourceBattery() { return Config.Instance().PowerSource.Equals("B"); }

        private bool sufficientBattery() { return (_batteryStatusWh > _minimumBatteryWh && (!_isMinimumBatteryReached || _batteryStatusWh > _sufficientBatteryWh)); }

        private bool lowBattery() { return (_batteryStatusWh < _minimumBatteryWh || _isMinimumBatteryReached); }

        private bool gridCutOffTimeReached() { return Config.Instance().NextGridCutOffTime < _currentTime; }
    }
}
