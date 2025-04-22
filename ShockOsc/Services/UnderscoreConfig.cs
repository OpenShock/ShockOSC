using CommandLine;
using Microsoft.Extensions.Logging;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Models;
using OpenShock.ShockOsc.Utils;

namespace OpenShock.ShockOsc.Services;

public sealed class UnderscoreConfig
{
    private readonly ILogger<UnderscoreConfig> _logger;
    private readonly OscClient _oscClient;
    private readonly ConfigManager _configManager;
    private readonly ShockOscData _dataLayer;

    public event Action? OnConfigUpdate;
    public event Action? OnGroupConfigUpdate;

    public UnderscoreConfig(ILogger<UnderscoreConfig> logger, OscClient oscClient, ConfigManager configManager,
        ShockOscData dataLayer)
    {
        _logger = logger;
        _oscClient = oscClient;
        _configManager = configManager;
        _dataLayer = dataLayer;
    }

    public bool KillSwitch { get; set; } = false;

    public bool GetProgramGroupFromGUID(Guid guid, out ProgramGroup? group)
    {
        return _dataLayer.ProgramGroups.TryGetValue(guid, out group);
    }

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
                    if (_configManager.Config.Behaviour.RandomIntensity == modeIntensity) return;
                    _configManager.Config.Behaviour.RandomIntensity = modeIntensity;
                    _configManager.Save();
                    OnConfigUpdate?.Invoke(); // update Ui
                }
                break;
            
            case "ModeDuration":
                if (value is bool modeDuration)
                {
                    if(_configManager.Config.Behaviour.RandomDuration == modeDuration) return;
                    _configManager.Config.Behaviour.RandomDuration = modeDuration;
                    _configManager.Save();
                    OnConfigUpdate?.Invoke(); // update Ui
                }
                break;
            
            case "Intensity":
                // 0..10sec
                if (value is float intensityFloat)
                {
                    var currentIntensity =
                        MathUtils.Saturate(_configManager.Config.Behaviour.FixedIntensity / 100f);
                    if (Math.Abs(intensityFloat - currentIntensity) < 0.001) return;

                    _configManager.Config.Behaviour.FixedIntensity =
                        Math.Clamp((byte)Math.Round(intensityFloat * 100), (byte)0, (byte)100);
                    _configManager.Config.Behaviour.RandomIntensity = false;
                    ValidateSettings();
                    _configManager.Save();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MinIntensity":
                // 0..100%
                if (value is float minIntensityFloat)
                {
                    var currentMinIntensity =
                        MathUtils.Saturate(_configManager.Config.Behaviour.IntensityRange.Min / 100f);
                    if (Math.Abs(minIntensityFloat - currentMinIntensity) < 0.001) return;

                    _configManager.Config.Behaviour.IntensityRange.Min =
                        MathUtils.ClampByte((byte)Math.Round(minIntensityFloat * 100), 0, 100);
                    _configManager.Config.Behaviour.RandomIntensity = true;
                    ValidateSettings();
                    _configManager.Save();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MaxIntensity":
                // 0..100%
                if (value is float maxIntensityFloat)
                {
                    var currentMaxIntensity =
                        MathUtils.Saturate(_configManager.Config.Behaviour.IntensityRange.Max / 100f);
                    if (Math.Abs(maxIntensityFloat - currentMaxIntensity) < 0.001) return;

                    _configManager.Config.Behaviour.IntensityRange.Max =
                        MathUtils.ClampByte((byte)Math.Round(maxIntensityFloat * 100), 0, 100);
                    _configManager.Config.Behaviour.RandomIntensity = true;
                    ValidateSettings();
                    _configManager.Save();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MinDuration":
                // 0..10sec
                if (value is float minDurationFloat)
                {
                    var currentMinDuration = _configManager.Config.Behaviour.DurationRange.Min / 10_000f;
                    if (Math.Abs(minDurationFloat - currentMinDuration) < 0.001) return;

                    _configManager.Config.Behaviour.DurationRange.Min =
                        MathUtils.ClampUShort((ushort)Math.Round(minDurationFloat * 10_000), 300, 30_000);
                    _configManager.Config.Behaviour.RandomDuration = true;
                    ValidateSettings();
                    _configManager.Save();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MaxDuration":
                // 0..10sec
                if (value is float maxDurationFloat)
                {
                    var currentMaxDuration = _configManager.Config.Behaviour.DurationRange.Max / 10_000f;
                    if (Math.Abs(maxDurationFloat - currentMaxDuration) < 0.001) return;

                    _configManager.Config.Behaviour.DurationRange.Max =
                        MathUtils.ClampUShort((ushort)Math.Round(maxDurationFloat * 10_000), 300, 30_000);
                    _configManager.Config.Behaviour.RandomDuration = true;
                    ValidateSettings();
                    _configManager.Save();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "Duration":
                // 0..10sec
                if (value is float durationFloat)
                {
                    var currentDuration = _configManager.Config.Behaviour.FixedDuration / 10000f;
                    if (Math.Abs(durationFloat - currentDuration) < 0.001) return;

                    _configManager.Config.Behaviour.FixedDuration =
                        MathUtils.ClampUShort((ushort)Math.Round(durationFloat * 10_000), 300, 10_000);
                    _configManager.Config.Behaviour.RandomDuration = false;
                    ValidateSettings();
                    _configManager.Save();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "CooldownTime":
                // 0..100sec
                if (value is float cooldownTimeFloat)
                {
                    var currentCooldownTime =
                        MathUtils.Saturate(_configManager.Config.Behaviour.CooldownTime / 100000f);
                    if (Math.Abs(cooldownTimeFloat - currentCooldownTime) < 0.001) return;

                    _configManager.Config.Behaviour.CooldownTime =
                        MathUtils.ClampUint((uint)Math.Round(cooldownTimeFloat * 100000), 0, 100000);
                    ValidateSettings();
                    _configManager.Save();
                    OnConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "HoldTime":
                // 0..1sec
                if (value is float holdTimeFloat)
                {
                    var currentHoldTime = MathUtils.Saturate(_configManager.Config.Behaviour.HoldTime / 1000f);
                    if (Math.Abs(holdTimeFloat - currentHoldTime) < 0.001) return;

                    _configManager.Config.Behaviour.HoldTime =
                        MathUtils.ClampUint((uint)Math.Round(holdTimeFloat * 1000), 0, 1000);
                    ValidateSettings();
                    _configManager.Save();
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
        //dont know if this is needed since all normal groups have a ConfigGroup, if it doesnt have it you are fucked anyway
        if (group.ConfigGroup == null) throw new ArgumentException("ConfigGroup is Null");

        switch (action)
        {
            case "ModeIntensity":
                if (value is bool modeIntensity)
                {
                    if (group.ConfigGroup.RandomIntensity == modeIntensity) return;
                    group.ConfigGroup.RandomIntensity = modeIntensity;
                    group.ConfigGroup.OverrideIntensity = true;
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }
                break;

            case "ModeDuration":
                if (value is bool modeDuration)
                {
                    if (group.ConfigGroup.RandomDuration == modeDuration) return;
                    group.ConfigGroup.RandomDuration = modeDuration;
                    group.ConfigGroup.OverrideDuration = true;
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }
                break;

            case "Intensity":
                // 0..10sec
                if (value is float intensityFloat)
                {
                    var currentIntensity =
                        MathUtils.Saturate(group.ConfigGroup.FixedIntensity / 100f);
                    if (Math.Abs(intensityFloat - currentIntensity) < 0.001) return;

                    group.ConfigGroup.FixedIntensity =
                        Math.Clamp((byte)Math.Round(intensityFloat * 100), (byte)0, (byte)100);
                    group.ConfigGroup.RandomIntensity = false;
                    group.ConfigGroup.OverrideIntensity = true;
                    ValidateGroupSettings(group.ConfigGroup);
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MinIntensity":
                // 0..100%
                if (value is float minIntensityFloat)
                {
                    var currentMinIntensity =
                        MathUtils.Saturate(group.ConfigGroup.IntensityRange.Min / 100f);
                    if (Math.Abs(minIntensityFloat - currentMinIntensity) < 0.001) return;

                    group.ConfigGroup.IntensityRange.Min =
                        MathUtils.ClampByte((byte)Math.Round(minIntensityFloat * 100), 0, 100);
                    group.ConfigGroup.RandomIntensity = true;
                    group.ConfigGroup.OverrideIntensity = true;
                    ValidateGroupSettings(group.ConfigGroup);
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MaxIntensity":
                // 0..100%
                if (value is float maxIntensityFloat)
                {
                    var currentMaxIntensity =
                        MathUtils.Saturate(group.ConfigGroup.IntensityRange.Max / 100f);
                    if (Math.Abs(maxIntensityFloat - currentMaxIntensity) < 0.001) return;

                    group.ConfigGroup.IntensityRange.Max =
                        MathUtils.ClampByte((byte)Math.Round(maxIntensityFloat * 100), 0, 100);
                    group.ConfigGroup.RandomIntensity = true;
                    group.ConfigGroup.OverrideIntensity = true;
                    ValidateGroupSettings(group.ConfigGroup);
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MinDuration":
                // 0..10sec
                if (value is float minDurationFloat)
                {
                    var currentMinDuration = group.ConfigGroup.DurationRange.Min / 10_000f;
                    if (Math.Abs(minDurationFloat - currentMinDuration) < 0.001) return;

                    group.ConfigGroup.DurationRange.Min =
                        MathUtils.ClampUShort((ushort)Math.Round(minDurationFloat * 10_000), 300, 30_000);
                    group.ConfigGroup.RandomDuration = true;
                    group.ConfigGroup.OverrideDuration = true;
                    ValidateGroupSettings(group.ConfigGroup);
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "MaxDuration":
                // 0..10sec
                if (value is float maxDurationFloat)
                {
                    var currentMaxDuration = group.ConfigGroup.DurationRange.Max / 10_000f;
                    if (Math.Abs(maxDurationFloat - currentMaxDuration) < 0.001) return;

                    group.ConfigGroup.DurationRange.Max =
                        MathUtils.ClampUShort((ushort)Math.Round(maxDurationFloat * 10_000), 300, 30_000);
                    group.ConfigGroup.RandomDuration = true;
                    group.ConfigGroup.OverrideDuration = true;
                    ValidateGroupSettings(group.ConfigGroup);
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "Duration":
                // 0..10sec
                if (value is float durationFloat)
                {
                    var currentDuration = group.ConfigGroup.FixedDuration / 10000f;
                    if (Math.Abs(durationFloat - currentDuration) < 0.001) return;

                    group.ConfigGroup.FixedDuration =
                        MathUtils.ClampUShort((ushort)Math.Round(durationFloat * 10_000), 300, 10_000);
                    group.ConfigGroup.RandomDuration = false;
                    group.ConfigGroup.OverrideDuration = true;
                    ValidateGroupSettings(group.ConfigGroup);
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "CooldownTime":
                // 0..100sec
                if (value is float cooldownTimeFloat)
                {
                    var currentCooldownTime =
                        MathUtils.Saturate(group.ConfigGroup.CooldownTime / 100000f);
                    if (Math.Abs(cooldownTimeFloat - currentCooldownTime) < 0.001) return;

                    group.ConfigGroup.CooldownTime =
                        MathUtils.ClampUint((uint)Math.Round(cooldownTimeFloat * 100000), 0, 100000);
                    group.ConfigGroup.OverrideCooldownTime = true;
                    ValidateGroupSettings(group.ConfigGroup);
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "OverrideCooldownTime":
                if (value is bool overrideCooldownTime)
                {
                    if (overrideCooldownTime == group.ConfigGroup.OverrideCooldownTime) return;
                    group.ConfigGroup.OverrideCooldownTime = overrideCooldownTime;
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "OverrideIntensity":
                if (value is bool overrideIntensity)
                {
                    if (overrideIntensity == group.ConfigGroup.OverrideIntensity) return;
                    group.ConfigGroup.OverrideIntensity = overrideIntensity;
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "OverrideDuration":
                if (value is bool overrideDuration)
                {
                    if (overrideDuration == group.ConfigGroup.OverrideDuration) return;
                    group.ConfigGroup.OverrideDuration = overrideDuration;
                    _configManager.Save();
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                }

                break;

            case "Paused":
                if (value is bool stateBool)
                {
                    if (group.Paused == stateBool) return;

                    group.Paused = stateBool;
                    OnGroupConfigUpdate?.Invoke(); // update Ui
                    _logger.LogInformation($"Paused state for {group.Name} set to: {group.Paused}");
                }

                break;
        }
    }

    private void ValidateSettings()
    {
        var intensityRange = _configManager.Config.Behaviour.IntensityRange;
        if (intensityRange.Min > intensityRange.Max) intensityRange.Max = intensityRange.Min;
        if (intensityRange.Max < intensityRange.Min) intensityRange.Min = intensityRange.Max;
        
        var durationRange = _configManager.Config.Behaviour.DurationRange;
        if (durationRange.Min > durationRange.Max) durationRange.Max = durationRange.Min;
        if (durationRange.Max < durationRange.Min) durationRange.Min = durationRange.Max;
    }

    private void ValidateGroupSettings(Group group)
    {
        var intensityRange = group.IntensityRange;
        if (intensityRange.Min > intensityRange.Max) intensityRange.Max = intensityRange.Min;
        if (intensityRange.Max < intensityRange.Min) intensityRange.Min = intensityRange.Max;

        var durationRange = group.DurationRange;
        if (durationRange.Min > durationRange.Max) durationRange.Max = durationRange.Min;
        if (durationRange.Max < durationRange.Min) durationRange.Min = durationRange.Max;
    }

    public async Task SendUpdateForAll()
    {
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/Paused", KillSwitch);
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/Paused", KillSwitch);
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MinIntensity",
            MathUtils.Saturate(_configManager.Config.Behaviour.IntensityRange.Min / 100f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MaxIntensity",
            MathUtils.Saturate(_configManager.Config.Behaviour.IntensityRange.Max / 100f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/Duration",
            MathUtils.Saturate(_configManager.Config.Behaviour.FixedDuration / 10000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/CooldownTime",
            MathUtils.Saturate(_configManager.Config.Behaviour.CooldownTime / 100000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/HoldTime",
            MathUtils.Saturate(_configManager.Config.Behaviour.HoldTime / 1000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/ModeIntensity",
            _configManager.Config.Behaviour.RandomIntensity);
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/ModeDuration",
            _configManager.Config.Behaviour.RandomDuration);
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/Intensity",
            MathUtils.Saturate(_configManager.Config.Behaviour.FixedIntensity / 100f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MinDuration",
            MathUtils.Saturate(_configManager.Config.Behaviour.DurationRange.Min / 10_000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MaxDuration",
            MathUtils.Saturate(_configManager.Config.Behaviour.DurationRange.Max / 10_000f));
        foreach (var (guid, programGroup) in _dataLayer.ProgramGroups)
        {
            if (programGroup.ConfigGroup != null)
            {
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/Paused", programGroup.Paused);
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/MinIntensity",
                    MathUtils.Saturate(programGroup.ConfigGroup.IntensityRange.Min / 100f));
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/MaxIntensity",
                    MathUtils.Saturate(programGroup.ConfigGroup.IntensityRange.Max / 100f));
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/Duration",
                    MathUtils.Saturate(programGroup.ConfigGroup.FixedDuration / 10000f));
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/CooldownTime",
                    MathUtils.Saturate(programGroup.ConfigGroup.CooldownTime / 100000f));
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/ModeIntensity",
                    programGroup.ConfigGroup.RandomIntensity);
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/ModeDuration",
                    programGroup.ConfigGroup.RandomDuration);
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/Intensity",
                    MathUtils.Saturate(programGroup.ConfigGroup.FixedIntensity / 100f));
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/MinDuration",
                    MathUtils.Saturate(programGroup.ConfigGroup.DurationRange.Min / 10_000f));
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/MaxDuration",
                    MathUtils.Saturate(programGroup.ConfigGroup.DurationRange.Max / 10_000f));
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/OverrideIntensity",
                    programGroup.ConfigGroup.OverrideIntensity);
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/OverrideDuration",
                    programGroup.ConfigGroup.OverrideDuration);
                await _oscClient.SendGameMessage($"/avatar/parameters/ShockOsc/_Config/{programGroup.Name}/OverrideCooldownTime",
                    programGroup.ConfigGroup.OverrideCooldownTime);
            }
        }
    }
}