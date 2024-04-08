using Microsoft.Extensions.Logging;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Utils;

namespace OpenShock.ShockOsc;

public sealed class UnderscoreConfig
{
    private readonly ILogger<UnderscoreConfig> _logger;
    private readonly OscClient _oscClient;
    private readonly ConfigManager _configManager;

    public UnderscoreConfig(ILogger<UnderscoreConfig> logger, OscClient oscClient, ConfigManager configManager)
    {
        _logger = logger;
        _oscClient = oscClient;
        _configManager = configManager;
    }
    
    public bool KillSwitch { get; set; } = false;
    
    public void HandleCommand(string parameterName, object?[] arguments)
    {
        var settingName = parameterName[8..];
        
        var settingPath = settingName.Split('/');
        if (settingPath.Length > 2)
        {
            _logger.LogWarning("Invalid setting path: {SettingPath}", settingPath);
            return;
        }

        if (settingPath.Length == 2)
        {
            var groupName = settingPath[0];
            var action = settingPath[1];
            if (!ShockOsc.ProgramGroups.Any(x => x.Value.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase)) && groupName != "_All")
            {
                _logger.LogWarning("Unknown shocker {Shocker}", groupName);
                _logger.LogDebug("Param: {Param}", action);
                return;
            }
            
            var group = ShockOsc.ProgramGroups.First(x => x.Value.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase));
            var value = arguments.ElementAtOrDefault(0);

            // TODO: support groups

            switch (action)
            {
                case "MinIntensity":
                    // 0..100%
                    if (value is float minIntensityFloat)
                    {
                        var currentMinIntensity = MathUtils.ClampFloat(_configManager.Config.Behaviour.IntensityRange.Min / 100f);
                        if (minIntensityFloat == currentMinIntensity) return;

                        _configManager.Config.Behaviour.IntensityRange.Min = MathUtils.ClampUint((uint)Math.Round(minIntensityFloat * 100), 0, 100);
                        ValidateSettings();
                        _configManager.Save();
                        ShockOsc.OnConfigUpdate?.Invoke(); // update Ui
                    }
                    break;

                case "MaxIntensity":
                    // 0..100%
                    if (value is float maxIntensityFloat)
                    {
                        var currentMaxIntensity = MathUtils.ClampFloat(_configManager.Config.Behaviour.IntensityRange.Max / 100f);
                        if (maxIntensityFloat == currentMaxIntensity) return;

                        _configManager.Config.Behaviour.IntensityRange.Max = MathUtils.ClampUint((uint)Math.Round(maxIntensityFloat * 100), 0, 100);
                        ValidateSettings();
                        _configManager.Save();
                        ShockOsc.OnConfigUpdate?.Invoke(); // update Ui
                    }
                    break;
                
                case "Duration":
                    // 0..10sec
                    if (value is float durationFloat)
                    {
                        var currentDuration = MathUtils.ClampFloat(_configManager.Config.Behaviour.FixedDuration / 10000f);
                        if (durationFloat == currentDuration) return;

                        _configManager.Config.Behaviour.FixedDuration = MathUtils.ClampUint((uint)Math.Round(durationFloat * 10000), 0, 10000);
                        ValidateSettings();
                        _configManager.Save();
                        ShockOsc.OnConfigUpdate?.Invoke(); // update Ui
                    }
                    break;

                case "CooldownTime":
                    // 0..100sec
                    if (value is float cooldownTimeFloat)
                    {
                        var currentCooldownTime = MathUtils.ClampFloat(_configManager.Config.Behaviour.CooldownTime / 100000f);
                        if (cooldownTimeFloat == currentCooldownTime) return;

                        _configManager.Config.Behaviour.CooldownTime = MathUtils.ClampUint((uint)Math.Round(cooldownTimeFloat * 100000), 0, 100000);
                        ValidateSettings();
                        _configManager.Save();
                        ShockOsc.OnConfigUpdate?.Invoke(); // update Ui
                    }
                    break;


                case "HoldTime":
                    // 0..1sec
                    if (value is float holdTimeFloat)
                    {
                        var currentHoldTime = MathUtils.ClampFloat(_configManager.Config.Behaviour.HoldTime / 1000f);
                        if (holdTimeFloat == currentHoldTime) return;

                        _configManager.Config.Behaviour.HoldTime = MathUtils.ClampUint((uint)Math.Round(holdTimeFloat * 1000), 0, 1000);
                        ValidateSettings();
                        _configManager.Save();
                        ShockOsc.OnConfigUpdate?.Invoke(); // update Ui
                    }
                    break;
            }
        }

        switch (settingName)
        {
            case "Paused":
                if (arguments.ElementAtOrDefault(0) is bool stateBool)
                {
                    if (KillSwitch == stateBool) return;

                    KillSwitch = stateBool;
                    _logger.LogInformation("Paused state set to: {KillSwitch}", KillSwitch);
                }
                break;
        }
    }

    private void ValidateSettings()
    {
        var intensityRange = _configManager.Config.Behaviour.IntensityRange;
        if (intensityRange.Min > intensityRange.Max) intensityRange.Max = intensityRange.Min;
        if(intensityRange.Max < intensityRange.Min) intensityRange.Min = intensityRange.Max;
        
    }

    public async Task SendUpdateForAll()
    {
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/Paused", KillSwitch);
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MinIntensity", MathUtils.ClampFloat(_configManager.Config.Behaviour.IntensityRange.Min / 100f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MaxIntensity", MathUtils.ClampFloat(_configManager.Config.Behaviour.IntensityRange.Max / 100f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/Duration", MathUtils.ClampFloat(_configManager.Config.Behaviour.FixedDuration / 10000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/CooldownTime", MathUtils.ClampFloat(_configManager.Config.Behaviour.CooldownTime / 100000f));
        await _oscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/HoldTime", MathUtils.ClampFloat(_configManager.Config.Behaviour.HoldTime / 1000f));
    }
}