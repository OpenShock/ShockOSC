using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Hub.Utils;
using OpenShock.ShockOsc.Utils;

namespace OpenShock.ShockOsc.Config;

public sealed class ConfigManager
{
    private static readonly string Path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ShockOSC\config.json";
    
    private readonly ILogger<ConfigManager> _logger;
    public ShockOscConfig Config { get; }

    public ConfigManager(ILogger<ConfigManager> logger)
    {
        _logger = logger;

        // Load config
        ShockOscConfig? config = null;
        
        _logger.LogInformation("Config file found, trying to load config from {Path}", Path);
        if (File.Exists(Path))
        {
            _logger.LogTrace("Config file exists");
            var json = File.ReadAllText(Path);
            if (!string.IsNullOrWhiteSpace(json))
            {
                _logger.LogTrace("Config file is not empty");
                try
                {
                    config = JsonSerializer.Deserialize<ShockOscConfig>(json, Options);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Error during deserialization/loading of config");
                    _logger.LogWarning("Attempting to move old config and generate a new one");
                    File.Move(Path, Path + ".old");
                }
            }
        }

        if (config != null)
        {
            Config = config;
            _logger.LogInformation("Successfully loaded config");
            return;
        }
        _logger.LogInformation("No config file found (does not exist or empty or invalid), generating new one at {Path}", Path);
        Config = new ShockOscConfig();
        SaveAsync().Wait();
        _logger.LogInformation("New configuration file generated!");
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(), new SemVersionJsonConverter() }
    };
    
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.LogTrace("Saving config");
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            await File.WriteAllTextAsync(Path, JsonSerializer.Serialize(Config, Options)).ConfigureAwait(false);
            _logger.LogInformation("Config saved");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while saving new config file");
        } 
        finally
        {
            _saveLock.Release();
        }
    }
    
    public void Save()
    {
        SaveAsync().Wait();
    }

    public void SaveFnf() => OsTask.Run(SaveAsync);
    
}