using Serilog;

namespace OpenShock.ShockOsc;

public static class UnderscoreConfig
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UnderscoreConfig));
    
    public static bool KillSwitch { get; set; } = false;
    
    public static void HandleCommand(string parameterName, object?[] arguments)
    {
        var settingName = parameterName[8..];
        
        var settingPath = settingName.Split('/');
        if (settingPath.Length > 2)
        {
            Logger.Warning("Invalid setting path: {SettingPath}", settingPath);
            return;
        }

        if (settingPath.Length == 2)
        {
            var shockerName = settingPath[0];
            var action = settingPath[1];
            if (!ShockOsc.Shockers.ContainsKey(shockerName) && shockerName != "_All")
            {
                Logger.Warning("Unknown shocker {Shocker}", shockerName);
                Logger.Debug("Param: {Param}", action);
                return;
            }
            
            var shocker = ShockOsc.Shockers[shockerName];
            var value = arguments.ElementAtOrDefault(0);

            // TODO: support groups

            switch (action)
            {
                case "MinIntensity":
                    // 0..100%
                    if (value is float minIntensityFloat)
                    {
                        var currentMinIntensity = ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.IntensityRange.Min / 100f);
                        if (minIntensityFloat == currentMinIntensity) return;

                        Config.ConfigInstance.Behaviour.IntensityRange.Min = ShockOsc.ClampUint((uint)Math.Round(minIntensityFloat * 100), 0, 100);
                        ValidateSettings();
                        Config.Save();
                        ShockOsc.OnConfigUpdate?.Invoke(); // update Ui
                    }
                    break;

                case "MaxIntensity":
                    // 0..100%
                    if (value is float maxIntensityFloat)
                    {
                        var currentMaxIntensity = ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.IntensityRange.Max / 100f);
                        if (maxIntensityFloat == currentMaxIntensity) return;

                        Config.ConfigInstance.Behaviour.IntensityRange.Max = ShockOsc.ClampUint((uint)Math.Round(maxIntensityFloat * 100), 0, 100);
                        ValidateSettings();
                        Config.Save();
                        ShockOsc.OnConfigUpdate?.Invoke(); // update Ui
                    }
                    break;
                
                case "Duration":
                    // 0..10sec
                    if (value is float durationFloat)
                    {
                        var currentDuration = ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.FixedDuration / 10000f);
                        if (durationFloat == currentDuration) return;

                        Config.ConfigInstance.Behaviour.FixedDuration = ShockOsc.ClampUint((uint)Math.Round(durationFloat * 10000), 0, 10000);
                        ValidateSettings();
                        Config.Save();
                        ShockOsc.OnConfigUpdate?.Invoke(); // update Ui
                    }
                    break;

                case "CooldownTime":
                    // 0..100sec
                    if (value is float cooldownTimeFloat)
                    {
                        var currentCooldownTime = ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.CooldownTime / 100000f);
                        if (cooldownTimeFloat == currentCooldownTime) return;

                        Config.ConfigInstance.Behaviour.CooldownTime = ShockOsc.ClampUint((uint)Math.Round(cooldownTimeFloat * 100000), 0, 100000);
                        ValidateSettings();
                        Config.Save();
                        ShockOsc.OnConfigUpdate?.Invoke(); // update Ui
                    }
                    break;


                case "HoldTime":
                    // 0..1sec
                    if (value is float holdTimeFloat)
                    {
                        var currentHoldTime = ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.HoldTime / 1000f);
                        if (holdTimeFloat == currentHoldTime) return;

                        Config.ConfigInstance.Behaviour.HoldTime = ShockOsc.ClampUint((uint)Math.Round(holdTimeFloat * 1000), 0, 1000);
                        ValidateSettings();
                        Config.Save();
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
                    Logger.Information("Paused state set to: {KillSwitch}", KillSwitch);
                }
                break;
        }
    }

    private static void ValidateSettings()
    {
        if (Config.ConfigInstance.Behaviour.IntensityRange.Min > Config.ConfigInstance.Behaviour.IntensityRange.Max)
        {
            Config.ConfigInstance.Behaviour.IntensityRange.Max = Config.ConfigInstance.Behaviour.IntensityRange.Min;
        }
    }

    public static async Task SendUpdateForAll()
    {
        await OscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/Paused", KillSwitch);
        await OscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MinIntensity", ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.IntensityRange.Min / 100f));
        await OscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/MaxIntensity", ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.IntensityRange.Max / 100f));
        await OscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/Duration", ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.FixedDuration / 10000f));
        await OscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/CooldownTime", ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.CooldownTime / 100000f));
        await OscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/_All/HoldTime", ShockOsc.ClampFloat(Config.ConfigInstance.Behaviour.HoldTime / 1000f));
    }
}