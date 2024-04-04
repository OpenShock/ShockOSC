using Serilog;
using OpenShock.SDK.CSharp;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.ShockOsc;

public static class OpenShockApi
{
    private static readonly ILogger Logger = Log.ForContext(typeof(OpenShockApi));
    
    private static OpenShockApiClient _client;

    static OpenShockApi()
    {
        SetupApiClient();
    }

    public static void SetupApiClient()
    {
        _client = new OpenShockApiClient(new ApiClientOptions
        {
            Server = Config.ConfigInstance.OpenShock.Backend,
            Token = Config.ConfigInstance.OpenShock.Token
        });
    }

    public static IReadOnlyCollection<ShockerResponse> Shockers = Array.Empty<ShockerResponse>();

    public static async Task GetShockers()
    {
        var response = await _client.GetOwnShockers();
        
        response.Switch(success =>
            {
                Shockers = success.Value.SelectMany(x => x.Shockers).ToArray();
                
                // re-populate config with previous data if present, this also deletes any shockers that are no longer present
                var shockerList = new Dictionary<Guid, Config.Conf.ShockerConf>();
                foreach (var shocker in Shockers)
                {
                    var enabled = true;
                
                    if (Config.ConfigInstance.OpenShock.Shockers.TryGetValue(shocker.Id, out var confShocker))
                    {
                        enabled = confShocker.Enabled;
                    }

                    shockerList.Add(shocker.Id, new Config.Conf.ShockerConf
                    {
                        Enabled = enabled
                    });
                }
                Config.ConfigInstance.OpenShock.Shockers = shockerList;
                Config.Save();
                ShockOsc.RefreshShockers();
            },
        error =>
        {
            Logger.Error("We are not authenticated with the OpenShock API!");
            // TODO: handle unauthenticated error
        });
    }
}