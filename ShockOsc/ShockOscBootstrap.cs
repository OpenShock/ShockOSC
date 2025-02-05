using System.Net;
using MudBlazor.Services;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Services;
using OscQueryLibrary;

namespace OpenShock.ShockOsc;

public static class ShockOscBootstrap
{
    public static void AddShockOscServices(this IServiceCollection services)
    {
        services.AddSingleton<ShockOscData>();

        services.AddSingleton<ConfigManager>();

        services.AddSingleton<OscClient>();


        services.AddSingleton<OscHandler>();
        services.AddSingleton<ChatboxService>();
        

        services.AddSingleton<ConfigUtils>();

        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<ConfigManager>();
            var listenAddress = config.Config.Osc.QuestSupport ? IPAddress.Any : IPAddress.Loopback;
            return new OscQueryServer("ShockOsc", listenAddress);
        });

        services.AddSingleton<Services.ShockOsc>();
        services.AddSingleton<UnderscoreConfig>();

        services.AddSingleton<MedalIcymiService>();
    }
    

    public static void StartShockOscServices(this IServiceProvider services, bool headless)
    {
        var config = services.GetRequiredService<ConfigManager>();
        
        // <---- Warmup ---->
        services.GetRequiredService<Services.ShockOsc>();
        
        if (config.Config.Osc.OscQuery) services.GetRequiredService<OscQueryServer>().Start();
    }
}