using System.Net;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using OpenShock.SDK.CSharp.Live;
using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Logging;
using OpenShock.ShockOsc.OscQueryLibrary;
using OpenShock.ShockOsc.Services;
using OpenShock.ShockOsc.Ui;
using Serilog;

namespace OpenShock.ShockOsc;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // <---- Services ---->

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

        builder.Services.AddSerilog(Log.Logger);
        
        builder.Services.AddSingleton<ShockOscData>();
        
        builder.Services.AddSingleton<ConfigManager>();

        builder.Services.AddSingleton<Updater>();
        builder.Services.AddSingleton<OscClient>();

        builder.Services.AddSingleton<OpenShockApi>();
        builder.Services.AddSingleton<OpenShockHubClient>();
        builder.Services.AddSingleton<BackendHubManager>();


        builder.Services.AddSingleton<OscHandler>();

        builder.Services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<ConfigManager>();
            var listenAddress = config.Config.Osc.QuestSupport ? IPAddress.Any : IPAddress.Loopback;
            return new OscQueryServer("ShockOsc", listenAddress, config);
        });
        
        builder.Services.AddSingleton<ShockOsc>();
        builder.Services.AddSingleton<UnderscoreConfig>();
        
        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();

        // <---- App ---->

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });


#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        var app = builder.Build();
            
        // <---- Warmup ---->
        app.Services.GetRequiredService<ShockOsc>();
        app.Services.GetRequiredService<OscQueryServer>().Start();
        
        return app;
    }
}