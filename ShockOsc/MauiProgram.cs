#if MAUI
using Microsoft.Maui.LifecycleEvents;
using OpenShock.ShockOsc.Config;
using MauiApp = OpenShock.ShockOsc.Ui.MauiApp;
using OpenShock.ShockOsc.Services.Pipes;
#if WINDOWS
using OpenShock.ShockOsc.Platforms.Windows;
#endif

namespace OpenShock.ShockOsc;

public static class MauiProgram
{
    private static ShockOscConfig? _config;
    private static PipeServerService? _pipeServerService;

    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();

        // <---- Services ---->

        builder.Services.AddShockOscServices();
        builder.Services.AddCommonBlazorServices();
        builder.Services.AddMauiBlazorWebView();

#if WINDOWS
        builder.Services.AddWindowsServices();

        builder.ConfigureLifecycleEvents(lifecycleBuilder =>
        {
            lifecycleBuilder.AddWindows(windowsLifecycleBuilder =>
            {
                windowsLifecycleBuilder.OnWindowCreated(window =>
                {
                    var appWindow = WindowUtils.GetAppWindow(window);

                    if (_pipeServerService != null)
                    {
                        _pipeServerService.OnMessageReceived += () =>
                        {
                            appWindow.ShowOnTop();

                            return Task.CompletedTask;
                        };
                    }

                    //When user execute the closing method, we can push a display alert. If user click Yes, close this application, if click the cancel, display alert will dismiss.
                    appWindow.Closing += async (s, e) =>
                    {
                        e.Cancel = true;

                        if (_config?.App.CloseToTray ?? false)
                        {
                            appWindow.Hide();
                            return;
                        }

                        if (Application.Current == null) return;

                        var page = Application.Current.Windows[0].Page;
                        
                        if(page == null) return;
                        
                        var result = await page.DisplayAlert(
                            "Close?",
                            "Do you want to close ShockOSC?",
                            "Yes",
                            "Cancel");

                        if (result) Application.Current.Quit();
                    };
                });
            });
        });
#endif

        // <---- App ---->

        builder
            .UseMauiApp<MauiApp>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        var app = builder.Build();

        _config = app.Services.GetRequiredService<ConfigManager>().Config;
        _pipeServerService = app.Services.GetRequiredService<PipeServerService>();

        app.Services.StartShockOscServices(false);

        return app;
    }
}
#endif