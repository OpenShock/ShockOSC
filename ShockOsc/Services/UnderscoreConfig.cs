﻿using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.ShockOSC.Config;
using OpenShock.ShockOSC.Models;
using OpenShock.ShockOSC.Utils;

namespace OpenShock.ShockOSC.Services;

public sealed class UnderscoreConfig
{
    private readonly ILogger<UnderscoreConfig> _logger;
    private readonly OscClient _oscClient;
    private readonly IModuleConfig<ShockOscConfig> _moduleConfig;
    private readonly ShockOscData _dataLayer;

    public event Action? OnConfigUpdate;

    public UnderscoreConfig(ILogger<UnderscoreConfig> logger, OscClient oscClient, IModuleConfig<ShockOscConfig> moduleConfig,
        ShockOscData dataLayer)
    {
        _logger = logger;
        _oscClient = oscClient;
        _moduleConfig = moduleConfig;
        _dataLayer = dataLayer;
    }

    public bool KillSwitch { get; set; } = false;

    public void HandleCommand(string parameterName, object?[] arguments)
    {
        var settingName = parameterName[8..];

        var settingPath = settingName.Split('/');
        if (settingPath.Length is > 2 or <= 0)
        {
            _logger.LogWarning("Invalid setting path: {SettingName}", settingName);
            return;
        }

        var value = arguments.ElementAtOrDefault(0);

        #region Legacy

        // Legacy Paused setting
        if (settingPath.Length == 1)
        {
            if (settingName != "Paused" || value is not bool stateBool || KillSwitch == stateBool) return;

            KillSwitch = stateBool;
            OnConfigUpdate?.Invoke(); // update Ui
            _logger.LogInformation("Paused state set to: {KillSwitch}", KillSwitch);
            return;
        }

        #endregion

        var groupName = settingPath[0];
        var action = settingPath[1];
        if (!_dataLayer.ProgramGroups.Any(x =>
                x.Value.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase)) && groupName != "_All")
        {
            _logger.LogWarning("Unknown shocker {Shocker}", groupName);
            _logger.LogDebug("Param: {Param}", action);
            return;
        }

        // Handle global config commands
        if (groupName == "_All")
        {
            HandleGlobalConfigCommand(action, value);
            return;
        }

        var group = _dataLayer.ProgramGroups.First(x =>
            x.Value.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase));

        HandleGroupConfigCommand(group.Value, action, value);
    }

    private void HandleGlobalConfigCommand(string action, object? value)
    {
        switch (action)
        {
            case "ModeIntensity":
                if (value is bool modeIntensity)
                {
                    if (_moduleConfig.Config.Behaviour.RandomIntensity == modeIntensity) return;
                    _moduleConfig.Config.Behaviour.RandomIntensity = modeIntensity;
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }
                break;
            
            case "ModeDuration":
                if (value is bool modeDuration)
                {
                    if(_moduleConfig.Config.Behaviour.RandomDuration == modeDuration) return;
                    _moduleConfig.Config.Behaviour.RandomDuration = modeDuration;
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }
                break;
            
            case "Intensity":
                // 0..10sec
                if (value is float intensityFloat)
                {
                    var currentIntensity =
                        MathUtils.Saturate(_moduleConfig.Config.Behaviour.FixedIntensity / 100f);
                    if (Math.Abs(intensityFloat - currentIntensity) < 0.001) return;

                    _moduleConfig.Config.Behaviour.FixedIntensity =
                        Math.Clamp((byte)Math.Round(intensityFloat * 100), (byte)0, (byte)100);
                    _moduleConfig.Config.Behaviour.RandomIntensity = false;
                    ValidateSettings();
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MinIntensity":
                // 0..100%
                if (value is float minIntensityFloat)
                {
                    var currentMinIntensity =
                        MathUtils.Saturate(_moduleConfig.Config.Behaviour.IntensityRange.Min / 100f);
                    if (Math.Abs(minIntensityFloat - currentMinIntensity) < 0.001) return;

                    _moduleConfig.Config.Behaviour.IntensityRange.Min =
                        MathUtils.ClampByte((byte)Math.Round(minIntensityFloat * 100), 0, 100);
                    _moduleConfig.Config.Behaviour.RandomIntensity = true;
                    ValidateSettings();
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MaxIntensity":
                // 0..100%
                if (value is float maxIntensityFloat)
                {
                    var currentMaxIntensity =
                        MathUtils.Saturate(_moduleConfig.Config.Behaviour.IntensityRange.Max / 100f);
                    if (Math.Abs(maxIntensityFloat - currentMaxIntensity) < 0.001) return;

                    _moduleConfig.Config.Behaviour.IntensityRange.Max =
                        MathUtils.ClampByte((byte)Math.Round(maxIntensityFloat * 100), 0, 100);
                    _moduleConfig.Config.Behaviour.RandomIntensity = true;
                    ValidateSettings();
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MinDuration":
                // 0..10sec
                if (value is float minDurationFloat)
                {
                    var currentMinDuration = _moduleConfig.Config.Behaviour.DurationRange.Min / 10_000f;
                    if (Math.Abs(minDurationFloat - currentMinDuration) < 0.001) return;

                    _moduleConfig.Config.Behaviour.DurationRange.Min =
                        MathUtils.ClampUShort((ushort)Math.Round(minDurationFloat * 10_000), 300, 30_000);
                    _moduleConfig.Config.Behaviour.RandomDuration = true;
                    ValidateSettings();
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MaxDuration":
                // 0..10sec
                if (value is float maxDurationFloat)
                {
                    var currentMaxDuration = _moduleConfig.Config.Behaviour.DurationRange.Max / 10_000f;
                    if (Math.Abs(maxDurationFloat - currentMaxDuration) < 0.001) return;

                    _moduleConfig.Config.Behaviour.DurationRange.Max =
                        MathUtils.ClampUShort((ushort)Math.Round(maxDurationFloat * 10_000), 300, 30_000);
                    _moduleConfig.Config.Behaviour.RandomDuration = true;
                    ValidateSettings();
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "Duration":
                // 0..10sec
                if (value is float durationFloat)
                {
                    var currentDuration = _moduleConfig.Config.Behaviour.FixedDuration / 10000f;
                    if (Math.Abs(durationFloat - currentDuration) < 0.001) return;

                    _moduleConfig.Config.Behaviour.FixedDuration =
                        MathUtils.ClampUShort((ushort)Math.Round(durationFloat * 10_000), 300, 10_000);
                    _moduleConfig.Config.Behaviour.RandomDuration = false;
                    ValidateSettings();
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "CooldownTime":
                // 0..100sec
                if (value is float cooldownTimeFloat)
                {
                    var currentCooldownTime =
                        MathUtils.Saturate(_moduleConfig.Config.Behaviour.CooldownTime / 100000f);
                    if (Math.Abs(cooldownTimeFloat - currentCooldownTime) < 0.001) return;

                    _moduleConfig.Config.Behaviour.CooldownTime =
                        MathUtils.ClampUint((uint)Math.Round(cooldownTimeFloat * 100000), 0, 100000);
                    ValidateSettings();
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "HoldTime":
                // 0..1sec
                if (value is float holdTimeFloat)
                {
                    var currentHoldTime = MathUtils.Saturate(_moduleConfig.Config.Behaviour.HoldTime / 1000f);
                    if (Math.Abs(holdTimeFloat - currentHoldTime) < 0.001) return;

                    _moduleConfig.Config.Behaviour.HoldTime =
                        MathUtils.ClampUint((uint)Math.Round(holdTimeFloat * 1000), 0, 1000);
                    ValidateSettings();
                    _moduleConfig.SaveDeferred();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "Paused":
                if (value is bool stateBool)
                {
                    if (KillSwitch == stateBool) return;

                    KillSwitch = stateBool;
                    OnConfigUpdate?.Invoke(); // update Ui
                    _logger.LogInformation("Paused state set to: {KillSwitch}", KillSwitch);
                }

                break;
        }
    }

    private void HandleGroupConfigCommand(ProgramGroup group, string action, object? value)
    {
        // TODO: support groups
    }

    private void ValidateSettings()
    {
        var intensityRange = _moduleConfig.Config.Behaviour.IntensityRange;
        if (intensityRange.Min > intensityRange.Max) intensityRange.Max = intensityRange.Min;
        if (intensityRange.Max < intensityRange.Min) intensityRange.Min = intensityRange.Max;
        
        var durationRange = _moduleConfig.Config.Behaviour.DurationRange;
        if (durationRange.Min > durationRange.Max) durationRange.Max = durationRange.Min;
        if (durationRange.Max < durationRange.Min) durationRange.Min = durationRange.Max;
    }

    public async Task SendUpdateForAll()
    {
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/Paused", KillSwitch);
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/Paused", KillSwitch);
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MinIntensity",
            MathUtils.Saturate(_moduleConfig.Config.Behaviour.IntensityRange.Min / 100f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MaxIntensity",
            MathUtils.Saturate(_moduleConfig.Config.Behaviour.IntensityRange.Max / 100f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/Duration",
            MathUtils.Saturate(_moduleConfig.Config.Behaviour.FixedDuration / 10000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/CooldownTime",
            MathUtils.Saturate(_moduleConfig.Config.Behaviour.CooldownTime / 100000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/HoldTime",
            MathUtils.Saturate(_moduleConfig.Config.Behaviour.HoldTime / 1000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/ModeIntensity",
            _moduleConfig.Config.Behaviour.RandomIntensity);
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/ModeDuration",
            _moduleConfig.Config.Behaviour.RandomDuration);
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/Intensity",
            MathUtils.Saturate(_moduleConfig.Config.Behaviour.FixedIntensity / 100f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MinDuration",
            MathUtils.Saturate(_moduleConfig.Config.Behaviour.DurationRange.Min / 10_000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MaxDuration",
            MathUtils.Saturate(_moduleConfig.Config.Behaviour.DurationRange.Max / 10_000f));
    }
}