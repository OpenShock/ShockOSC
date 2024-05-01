using System.Diagnostics;
using System.Net;
using Microsoft.Maui.LifecycleEvents;
using MudBlazor.Services;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Logging;
using OpenShock.ShockOsc.OscQueryLibrary;
using OpenShock.ShockOsc.Services;
using OpenShock.ShockOsc.Utils;
using Serilog;
using MauiApp = OpenShock.ShockOsc.Ui.MauiApp;
using Rect = OpenShock.ShockOsc.Utils.Rect;

namespace OpenShock.ShockOsc;

public static class MauiProgram
{
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_BORDER = 0x00800000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_SIZEBOX = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_THICKFRAME = 0x00040000;
    
    
    private static ShockOscConfig? _config;

    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();

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

        builder.Services.AddSingleton<LiveControlManager>();

        builder.Services.AddSingleton<OscHandler>();

        builder.Services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<ConfigManager>();
            var listenAddress = config.Config.Osc.QuestSupport ? IPAddress.Any : IPAddress.Loopback;
            return new OscQueryServer("ShockOsc", listenAddress, config);
        });

        builder.Services.AddSingleton<Services.ShockOsc>();
        builder.Services.AddSingleton<UnderscoreConfig>();

#if WINDOWS
        builder.ConfigureLifecycleEvents(lifecycleBuilder =>
        {
            lifecycleBuilder.AddWindows(windowsLifecycleBuilder =>
            {
                windowsLifecycleBuilder.OnWindowCreated(window =>
                {
                    //use Microsoft.UI.Windowing functions for window
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
                    
                    // var style = WindowUtils.GetWindowLongPtrA(handle, (int)WindowLongFlags.GWL_STYLE);
                    //
                    // style &= ~WS_CAPTION; // Remove the title bar
                    // style |= WS_THICKFRAME; // Add thick frame for resizing
                    //
                    // WindowUtils.SetWindowLongPtrA(handle, (int)WindowLongFlags.GWL_STYLE,  style);
                    //
                    // var reff = new Rect();
                    // WindowUtils.AdjustWindowRectEx(ref reff, style, false, 0);
                    // reff.top = 6000;
                    // reff.left *= -1;
                    //
                    // var margins = new Margins
                    // {
                    //     cxLeftWidth = 0,
                    //     cxRightWidth = 0,
                    //     cyTopHeight = 0,
                    //     cyBottomHeight = 0
                    // };
                    //
                    // WindowUtils.DwmExtendFrameIntoClientArea(handle, ref margins);
                    //
                    // WindowUtils.SetWindowPos(handle,  0, 0, 0, 0, 0x0040 | 0x0002 | 0x0001 | 0x0020);
                    //
                    
                    //When user execute the closing method, we can push a display alert. If user click Yes, close this application, if click the cancel, display alert will dismiss.
                    appWindow.Closing += async (s, e) =>
                    {
                        e.Cancel = true;

                        if (_config?.App.CloseToTray ?? false)
                        {
                            appWindow.Hide();
                            return;
                        }

                        var result = await Application.Current.MainPage.DisplayAlert(
                            "Close?",
                            "Do you want to close ShockOSC?",
                            "Yes",
                            "Cancel");

                        if (result) Application.Current.Quit();
                    };
                });
            });
        });


        builder.Services.AddSingleton<ITrayService, WindowsTrayService>();
#endif

        builder.Services.AddSingleton<StatusHandler>();
        
        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();
        
        // <---- App ---->

        builder
            .UseMauiApp<MauiApp>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });


#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        var app = builder.Build();

        _config = app.Services.GetRequiredService<ConfigManager>().Config;

        app.Services.GetService<ITrayService>()?.Initialize();

        // <---- Warmup ---->
        app.Services.GetRequiredService<Services.ShockOsc>();
        app.Services.GetRequiredService<OscQueryServer>().Start();

        var updater = app.Services.GetRequiredService<Updater>();
        OsTask.Run(updater.CheckUpdate);

        return app;
    }
}