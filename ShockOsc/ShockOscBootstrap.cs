using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Logging;
using OpenShock.ShockOsc.OscQueryLibrary;
using OpenShock.ShockOsc.Services;
using OpenShock.ShockOsc.Services.Pipes;
using OpenShock.ShockOsc.Utils;
using Serilog;

namespace OpenShock.ShockOsc;

public static class ShockOscBootstrap
{
    public static void AddShockOscServices(this IServiceCollection services)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Filter.ByExcluding(ev =>
                ev.Exception is InvalidDataException a && a.Message.StartsWith("Invocation provides")).Filter
            .ByExcluding(x => x.MessageTemplate.Text.StartsWith("Failed to find handler for"))
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
            .WriteTo.UiLogSink()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

        // ReSharper disable once RedundantAssignment
        var isDebug = Environment.GetCommandLineArgs()
            .Any(x => x.Equals("--debug", StringComparison.InvariantCultureIgnoreCase));

#if DEBUG
        isDebug = true;
#endif
        if (isDebug)
        {
            Console.WriteLine("Debug mode enabled");
            loggerConfiguration.MinimumLevel.Verbose();
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        services.AddSerilog(Log.Logger);

        services.AddMemoryCache();

        services.AddSingleton<PipeServerService>();
        
        services.AddSingleton<ShockOscData>();

        services.AddSingleton<ConfigManager>();

        services.AddSingleton<Updater>();
        services.AddSingleton<OscClient>();

        services.AddSingleton<OpenShockApi>();
        services.AddSingleton<OpenShockHubClient>();
        services.AddSingleton<BackendHubManager>();

        services.AddSingleton<LiveControlManager>();

        services.AddSingleton<OscHandler>();

        services.AddSingleton<AuthService>();

        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<ConfigManager>();
            var listenAddress = config.Config.Osc.QuestSupport ? IPAddress.Any : IPAddress.Loopback;
            return new OscQueryServer("ShockOsc", listenAddress, config);
        });

        services.AddSingleton<Services.ShockOsc>();
        services.AddSingleton<UnderscoreConfig>();

#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddSingleton<StatusHandler>();

        services.AddMudServices();
        services.AddMauiBlazorWebView();
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


        // <---- Warmup ---->
        services.GetRequiredService<Services.ShockOsc>();
        services.GetRequiredService<OscQueryServer>().Start();
        services.GetRequiredService<PipeServerService>().StartServer();

        var updater = services.GetRequiredService<Updater>();
        OsTask.Run(updater.CheckUpdate);
    }
}