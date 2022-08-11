﻿using System;
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
        private decimal _batteryStatusWh;
        private bool _isMinimumBatteryReached = false;

        private decimal _minimumBatteryWh;
        private decimal _sufficientBatteryWh;
        private decimal _fullBatteryWh;
        private decimal _batteryChargeEnergyWh;
        private decimal _solarGeneratedWh;

        public Inverter()
        {
            _rawData = new RawDataDto();
            _random = new Random();
            _gapBetweenTwoReadingsSec = Config.Instance().TimeGapSec;
            _batteryStatusWh = Config.Instance().InitialBatteryPerc * Config.Instance().BatteryCapacitykWh * 10;

            _minimumBatteryWh = Config.Instance().MinimumBatteryPerc * Config.Instance().BatteryCapacitykWh * 10;
            _sufficientBatteryWh = _minimumBatteryWh * 1.1M;
            _fullBatteryWh = Config.Instance().BatteryCapacitykWh * 1000;
            _batteryChargeEnergyWh = Config.Instance().BatteryMaximumChargeWatt * (Config.Instance().TimeGapSec / 3600M);

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
            _rawData.SolarOutputWatts = getCurrentSolarOutput(_rawData.LoggedAt.Hour).Round(Config.Instance().RoundUpto);

            _solarGeneratedWh = calculateConsumptionOrGeneration(_rawData.SolarOutputWatts);

            _rawData.LoadWatts = getCurrentLoad(_rawData.LoggedAt.Hour).Round(Config.Instance().RoundUpto);
            _rawData.ConsumptionWh = calculateConsumptionOrGeneration(_rawData.LoadWatts);
            _rawData.TimeIntervalSec = Config.Instance().TimeGapSec;
            _rawData.CustomerID = 610;
            await updateBatteryStatus(_rawData.ConsumptionWh);

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
            if (_isDataGenerationMode)
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
        private async Task updateBatteryStatus(decimal consumptionWh)
        {
            if (gridCutOffTimeReached())
            {
                while (Config.Instance().NextGridCutOffTime < DateTime.Now)
                {
                    Config.Instance().NextGridCutOffTime = Config.Instance().NextGridCutOffTime.AddDays(1);
                }
                await ServerUpload.UpdateNextGridCutOffTimeDB(Config.Instance());

                Config.Instance().PowerSource = "B";
            }

            if (isSolarPowerGenerated())
            {
                await updateBatteryWhenSolar(consumptionWh);
            }
            else
            {
                await updateBatteryWhenNoSolar(consumptionWh);
            }
        }

        private async Task updateBatteryWhenSolar(decimal consumptionWh)
        {
            if (sufficientBattery())
            {
                _batteryStatusWh -= consumptionWh - _solarGeneratedWh;
                Config.Instance().PowerSource = "B+S";
                _isMinimumBatteryReached = false;
            }
            else if (batteryFull())
            {
                if (_solarGeneratedWh >= consumptionWh)
                    _solarGeneratedWh = consumptionWh;
                else
                    _batteryStatusWh -= consumptionWh - _solarGeneratedWh;

                _isMinimumBatteryReached = false;
                Config.Instance().PowerSource = "S";
                Config.Instance().NextGridCutOffTime = Config.Instance().NextGridCutOffTime.AddHours(-1);
                await ServerUpload.UpdateNextGridCutOffTimeDB(Config.Instance());
            }
            else if (lowBattery())
            {
                _batteryStatusWh += _batteryChargeEnergyWh;
                Config.Instance().PowerSource = "G+S";

                if (_solarGeneratedWh >= consumptionWh)
                    _solarGeneratedWh = consumptionWh;

                _isMinimumBatteryReached = true;
                Config.Instance().NextGridCutOffTime = Config.Instance().NextGridCutOffTime.AddHours(1);
                await ServerUpload.UpdateNextGridCutOffTimeDB(Config.Instance());
            }
        }

        private async Task updateBatteryWhenNoSolar(decimal consumptionWh)
        {
            if (isPowerSourceBattery())
            {
                if (_batteryStatusWh > _minimumBatteryWh && (!_isMinimumBatteryReached || _batteryStatusWh > _sufficientBatteryWh))
                {
                    _batteryStatusWh -= consumptionWh;
                    _isMinimumBatteryReached = false;
                }
                else if (_batteryStatusWh < _minimumBatteryWh || _isMinimumBatteryReached)
                {
                    _batteryStatusWh += _batteryChargeEnergyWh;
                    Config.Instance().PowerSource = "G";
                    _isMinimumBatteryReached = true;
                    Config.Instance().NextGridCutOffTime = Config.Instance().NextGridCutOffTime.AddHours(1);
                    await ServerUpload.UpdateNextGridCutOffTimeDB(Config.Instance());
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

        private bool gridCutOffTimeReached() { return Config.Instance().NextGridCutOffTime < DateTime.Now; }
    }
}
