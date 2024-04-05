using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Live;
using Serilog;

namespace OpenShock.ShockOsc.Backend;

public sealed class BackendLiveApiManager
{
    private readonly ILogger<BackendLiveApiManager> _logger;
    private readonly ShockOscConfigManager.ShockOscConfig _config;
    private readonly OpenShockApiLiveClient _openShockApiLiveClient;

    public BackendLiveApiManager(ILogger<BackendLiveApiManager> logger, ShockOscConfigManager.ShockOscConfig config, OpenShockApiLiveClient openShockApiLiveClient)
    {
        _logger = logger;
        _config = config;
        _openShockApiLiveClient = openShockApiLiveClient;
    }


    public async Task SetupLiveClient()
    {
        await _openShockApiLiveClient.Setup(new ApiLiveClientOptions()
        {
            Token = _config.OpenShock.Token,
            Server = _config.OpenShock.Backend,
            ConfigureLogging = builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddSerilog();
            }
        });
        //await _openShockApiLiveClient.StartAsync();
    }
    
}