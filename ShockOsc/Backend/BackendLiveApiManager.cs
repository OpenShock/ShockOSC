using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Live;
using OpenShock.ShockOsc.Config;
using Serilog;

namespace OpenShock.ShockOsc.Backend;

public sealed class BackendLiveApiManager
{
    private readonly ILogger<BackendLiveApiManager> _logger;
    private readonly ConfigManager _configManager;
    private readonly OpenShockApiLiveClient _openShockApiLiveClient;

    public BackendLiveApiManager(ILogger<BackendLiveApiManager> logger, ConfigManager configManager, OpenShockApiLiveClient openShockApiLiveClient)
    {
        _logger = logger;
        _configManager = configManager;
        _openShockApiLiveClient = openShockApiLiveClient;
    }


    public async Task SetupLiveClient()
    {
        await _openShockApiLiveClient.Setup(new ApiLiveClientOptions()
        {
            Token = _configManager.Config.OpenShock.Token,
            Server = _configManager.Config.OpenShock.Backend,
            ConfigureLogging = builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddSerilog();
            }
        });
    }
    
}