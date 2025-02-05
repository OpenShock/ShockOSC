using System.Net;
using MudBlazor.Services;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Logging;
using OpenShock.ShockOsc.Services;
using OpenShock.ShockOsc.Services.Pipes;
using OpenShock.ShockOsc.Utils;
using OscQueryLibrary;
using Serilog;

namespace OpenShock.ShockOsc;

public static class ShockOscBootstrap
{
    public static void AddShockOscServices(this IServiceCollection services)
    {
        services.AddSingleton<ShockOscData>();

        services.AddSingleton<ConfigManager>();

        services.AddSingleton<Updater>();
        services.AddSingleton<OscClient>();

        services.AddSingleton<LiveControlManager>();

        services.AddSingleton<OscHandler>();
        services.AddSingleton<ChatboxService>();

        services.AddSingleton<AuthService>();

        services.AddSingleton<ConfigUtils>();

        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<ConfigManager>();
            var listenAddress = config.Config.Osc.QuestSupport ? IPAddress.Any : IPAddress.Loopback;
            return new OscQueryServer("ShockOsc", listenAddress);
        });

        services.AddSingleton<Services.ShockOsc>();
        services.AddSingleton<UnderscoreConfig>();

        services.AddSingleton<StatusHandler>();
        services.AddSingleton<MedalIcymiService>();
    }

    public static void AddCommonBlazorServices(this IServiceCollection services)
    {
#if DEBUG_WINDOWS || DEBUG_PHOTINO || DEBUG_WEB
        services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddMudServices();
    }

    public static void StartShockOscServices(this IServiceProvider services, bool headless)
    {
        #region SystemTray

#if WINDOWS
        if (headless)
        {
            var applicationThread = new Thread(() =>
            {
                services.GetService<ITrayService>()?.Initialize();
                System.Windows.Forms.Application.Run();
            });
            applicationThread.Start();
        }
        else services.GetService<ITrayService>()?.Initialize();
#else
        services.GetService<ITrayService>()?.Initialize();
#endif

        #endregion

        var config = services.GetRequiredService<ConfigManager>();


        // <---- Warmup ---->
        services.GetRequiredService<Services.ShockOsc>();
        services.GetRequiredService<PipeServerService>().StartServer();
        
        if (config.Config.Osc.OscQuery) services.GetRequiredService<OscQueryServer>().Start();

        var updater = services.GetRequiredService<Updater>();
        OsTask.Run(updater.CheckUpdate);
    }
}