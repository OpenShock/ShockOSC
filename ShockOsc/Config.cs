using System.Text.Json;
using Serilog;

namespace ShockLink.ShockOsc;

public static class Config
{
    private static Conf? _internalConfig;
    private static readonly string Path = Directory.GetCurrentDirectory() + "/config.json";
    public static Conf ConfigInstance
    {
        get
        {
            TryLoad();
            return _internalConfig!;
        }
    }

    static Config()
    {
        TryLoad();
    }

    private static void TryLoad()
    {
        if (_internalConfig != null) return;
        Log.Information("Config file found, trying to load config from {Path}", Path);
        if (File.Exists(Path))
        {
            Log.Verbose("Config file exists");
            var json = File.ReadAllText(Path);
            if (!string.IsNullOrWhiteSpace(json))
            {
                Log.Verbose("Config file is not empty");
                try
                {
                    _internalConfig = JsonSerializer.Deserialize<Conf>(json);
                    Log.Information("Successfully loaded config");
                }
                catch (JsonException e)
                {
                    Log.Fatal(e, "Error during deserialization/loading of config");
                    return;
                }
            }
        }

        if (_internalConfig != null) return;
        Log.Information("No valid config file found, generating new one at {Path}", Path);
        _internalConfig = GetDefaultConfig();
        Save();
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void Save()
    {
        Log.Information("Saving config");
        try
        {
            File.WriteAllText(Path, JsonSerializer.Serialize(_internalConfig, Options));
        }
        catch (Exception e)
        {
            Log.Error(e, "Error occurred while saving new config file");
        }
    }

    private static Conf GetDefaultConfig() => new()
    {
        Osc = new Conf.OscConf
        {
            Chatbox = true,
            Hoscy = false,
            ReceivePort = 9001,
            SendPort = 9000,
        },
        Behaviour = new Conf.BehaviourConf
        {
            RandomDuration = true,
            RandomIntensity = true,
            RandomDurationRange = new JsonRange { Min = 1, Max = 5 },
            RandomIntensityRange = new JsonRange { Min = 1, Max = 100 },
            FixedDuration = 2,
            FixedIntensity = 50,
            CooldownTime = 5000,
            HoldTime = 250
        },
        ShockLink = new Conf.ShockLinkConf
        {
            Shockers = new Dictionary<string, Guid>(),
            Type = ControlType.Shock,
            BaseUri = new Uri("wss://api.shocklink.net"),
            ApiToken = "SET THIS TO YOUR SHOCKLINK API TOKEN"
        }
    };

    public class Conf
    {
        public required OscConf Osc { get; set; }
        public required BehaviourConf Behaviour { get; set; }
        public required ShockLinkConf ShockLink { get; set; }

        public class OscConf
        {
            public required bool Chatbox { get; set; }
            public required bool Hoscy { get; set; }
            public required uint ReceivePort { get; set; }
            public required uint SendPort { get; set; }
        }

        public class BehaviourConf
        {
            public required bool RandomIntensity { get; set; }
            public required bool RandomDuration { get; set; }
            public required JsonRange RandomIntensityRange { get; set; }
            public required JsonRange RandomDurationRange { get; set; }
            public required byte FixedIntensity { get; set; }
            public required uint FixedDuration { get; set; }
            public required uint HoldTime { get; set; }
            public required uint CooldownTime { get; set; }
        }

        public class ShockLinkConf
        {
            public required ControlType Type { get; set; }
            public required Uri BaseUri { get; set; }
            public required string ApiToken { get; set; }
            public required IReadOnlyDictionary<string, Guid> Shockers { get; set; }
        }
    }
}