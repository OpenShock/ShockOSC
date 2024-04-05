using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.ShockOsc.Backend;

public sealed class OpenShockApi
{
    private readonly ILogger<OpenShockApi> _logger;
    private readonly ShockOscConfigManager.ShockOscConfig _config;
    private OpenShockApiClient _client;

    public OpenShockApi(ILogger<OpenShockApi> logger, ShockOscConfigManager.ShockOscConfig config)
    {
        _logger = logger;
        _config = config;
        SetupApiClient();
    }

    public void SetupApiClient()
    {
        _client = new OpenShockApiClient(new ApiClientOptions
        {
            Server = _config.OpenShock.Backend,
            Token = _config.OpenShock.Token
        });
    }

    public IReadOnlyCollection<ShockerResponse> Shockers = Array.Empty<ShockerResponse>();

    public async Task RefreshShockers()
    {
        var response = await _client.GetOwnShockers();
        
        response.Switch(success =>
            {
                Shockers = success.Value.SelectMany(x => x.Shockers).ToArray();
                
                // re-populate config with previous data if present, this also deletes any shockers that are no longer present
                var shockerList = new Dictionary<Guid, ShockOscConfigManager.ShockOscConfig.ShockerConf>();
                foreach (var shocker in Shockers)
                {
                    var enabled = true;
                
                    if (ShockOscConfigManager.ConfigInstance.OpenShock.Shockers.TryGetValue(shocker.Id, out var confShocker))
                    {
                        enabled = confShocker.Enabled;
                    }

                    shockerList.Add(shocker.Id, new ShockOscConfigManager.ShockOscConfig.ShockerConf
                    {
                        Enabled = enabled
                    });
                }
                ShockOscConfigManager.ConfigInstance.OpenShock.Shockers = shockerList;
                ShockOscConfigManager.Save();
                ShockOsc.RefreshShockers();
            },
        error =>
        {
            _logger.LogError("We are not authenticated with the OpenShock API!");
            // TODO: handle unauthenticated error
        });
    }
}