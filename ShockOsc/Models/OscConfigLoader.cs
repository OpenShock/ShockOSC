using System.Text.Json;
using Serilog;

namespace ShockLink.ShockOsc.Models;

public static class OscConfigLoader
{
    public static void OnAvatarChange(string? avatarId)
    {
        ShockOsc.Shockers.Clear();
        var parameterCount = 0;
        var avatarConfig = ReadOscConfigFile(avatarId);
        if (avatarConfig == null)
        {
            Log.Error("Failed to read avatar config file for {AvatarId}", avatarId);
            return;
        }

        foreach (var param in avatarConfig.parameters)
        {
            if (!param.name.StartsWith("ShockOsc/"))
                continue;
            
            var paramName = param.name.Substring(9, param.name.Length - 9);
            var lastUnderscoreIndex = paramName.LastIndexOf('_') + 1;
            var action = string.Empty;
            if (lastUnderscoreIndex != 0)
                action = paramName.Substring(lastUnderscoreIndex, paramName.Length - lastUnderscoreIndex);
            
            var shockerName = paramName;
            if (ShockOsc.ShockerParams.Contains(action))
                shockerName = paramName.Substring(0, lastUnderscoreIndex - 1);
            
            if (!ShockOsc.Shockers.ContainsKey(shockerName))
            {
                if (!Config.ConfigInstance.ShockLink.Shockers.ContainsKey(shockerName))
                {
                    Log.Warning("Unknown shocker {Shocker}", shockerName);
                    continue;
                }
                ShockOsc.Shockers.TryAdd(shockerName, new Shocker(Config.ConfigInstance.ShockLink.Shockers[shockerName]));
            }
            
            var shocker = ShockOsc.Shockers[shockerName];
            switch (action)
            {
                case "Cooldown":
                    if (param.input.type != "Bool") break;
                    shocker.HasCooldownParam = true;
                    parameterCount++;
                    break;
                case "Active":
                    if (param.input.type != "Bool") break;
                    shocker.HasActiveParam = true;
                    parameterCount++;
                    break;
                case "Intensity":
                    if (param.input.type != "Float") break;
                    shocker.HasIntensityParam = true;
                    parameterCount++;
                    break;
                case "Stretch":
                case "IsGrabbed":
                case "":
                    parameterCount++;
                    break;
            }
        }
        
        Log.Information("Loaded avatar config for {AvatarId} with {ParamCount} parameters", avatarId, parameterCount);
    }

    private static AvatarConfigJson? ReadOscConfigFile(string? avatarId)
    {
        var latestWriteTime = DateTime.MinValue;
        AvatarConfigJson? aviConfig = null;
        
        var oscDirPath = Path.Combine($"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}Low", "VRChat\\VRChat\\OSC");
        if (!Directory.Exists(oscDirPath))
            return null;

        var userDir = Directory.GetDirectories(oscDirPath);
        foreach (var user in userDir)
        {
            var aviDirPath = Path.Combine(user, "Avatars");
            if (!Directory.Exists(aviDirPath))
                continue;

            var aviFiles = Directory.GetFiles(aviDirPath);
            foreach (var aviFile in aviFiles)
            {
                var configText = File.ReadAllText(aviFile);
                var config = JsonSerializer.Deserialize<AvatarConfigJson>(configText);
                if (config == null || config.id != avatarId)
                    continue;
                
                var lastWriteTime = File.GetLastWriteTime(aviFile);
                if (lastWriteTime <= latestWriteTime)
                    continue;
                
                latestWriteTime = lastWriteTime;
                aviConfig = config;
            }
        }
        
        return aviConfig;
    }
}