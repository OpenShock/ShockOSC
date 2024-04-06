using System.Net;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using OpenShock.SDK.CSharp.Live;
using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Logging;
using OpenShock.ShockOsc.OscQueryLibrary;
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
        var isDebug = Environment.GetCommandLineArgs().Any(x => x.Equals("--debug", StringComparison.InvariantCultureIgnoreCase));
        
#if DEBUG
        isDebug = true;
#endif
        if (isDebug)
        {
            loggerConfiguration.MinimumLevel.Debug();
        }

        Log.Logger = loggerConfiguration.CreateLogger();
        
        builder.Services.AddSerilog(Log.Logger);

        builder.Services.AddSingleton(ShockOscConfigManager.ConfigInstance);

        builder.Services.AddSingleton<OscClient>();
        
        builder.Services.AddSingleton<OpenShockApi>();
        builder.Services.AddSingleton<OpenShockApiLiveClient>();
        builder.Services.AddSingleton<BackendLiveApiManager>();

        
        builder.Services.AddSingleton<OscHandler>();
        
        builder.Services.AddSingleton<ShockOsc>();

        builder.Services.AddSingleton<UnderscoreConfig>();
        
        var listenAddress = ShockOscConfigManager.ConfigInstance.Osc.QuestSupport ? IPAddress.Any : IPAddress.Loopback; 
        builder.Services.AddSingleton<OscQueryServer>(provider =>
        {
            var shockOsc = provider.GetRequiredService<ShockOsc>();
            return new OscQueryServer("ShockOsc", listenAddress, shockOsc.FoundVrcClient, shockOsc.OnAvatarChange);
        });
        
        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();
        
        // <---- App ---->
        
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });
        

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        
        return builder.Build();;
    }
}