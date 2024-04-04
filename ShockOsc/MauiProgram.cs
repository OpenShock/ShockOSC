using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using OpenShock.ShockOsc.Logging;
using OpenShock.ShockOsc.Ui;
using Serilog;

namespace OpenShock.ShockOsc;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Filter.ByExcluding(ev =>
                ev.Exception is InvalidDataException a && a.Message.StartsWith("Invocation provides")).Filter.ByExcluding(x => x.MessageTemplate.Text.StartsWith("Failed to find handler for"))
            .WriteTo.UiLogSink()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // ReSharper disable once RedundantAssignment
        var isDebug = false;
#if DEBUG
        isDebug = true;
#endif
        if (isDebug)
        {
            Log.Information("Debug logging enabled");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Filter.ByExcluding(ev =>
                    ev.Exception is InvalidDataException a && a.Message.StartsWith("Invocation provides")).Filter.ByExcluding(x => x.MessageTemplate.Text.StartsWith("Failed to find handler for"))
                .WriteTo.UiLogSink()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
        
        builder.Services.AddSerilog(Log.Logger);

        var mauiApp = builder.Build();
        
        return mauiApp;
    }
}