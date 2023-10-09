using Serilog;

namespace OpenShock.ShockOsc;

public static class UnderscoreConfig
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UnderscoreConfig));
    
    public static bool KillSwitch { get; set; } = false;
    
    public static void HandleCommand(string parameterName, object?[] arguments)
    {
        var settingName = parameterName[8..];
        switch (settingName)
        {
            case "Paused":
                if (arguments.ElementAtOrDefault(0) is bool stateBool)
                {
                    KillSwitch = stateBool;
                    Logger.Information("Paused state set to: {KillSwitch}", KillSwitch);
                }
                break;
        }
    }

    public static async Task SendUpdateForAll()
    {
        await OscClient.SendGameMessage("/avatar/parameters/ShockOsc/_Config/Paused", KillSwitch);
    }
}