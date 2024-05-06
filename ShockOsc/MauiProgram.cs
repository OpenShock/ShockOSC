#if MAUI
using Microsoft.Maui.LifecycleEvents;
using OpenShock.ShockOsc.Config;
using MauiApp = OpenShock.ShockOsc.Ui.MauiApp;
using Microsoft.UI;

namespace OpenShock.ShockOsc;

public static class MauiProgram
{
    private static ShockOscConfig? _config;

    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();

        // <---- Services ---->

        builder.Services.AddShockOscServices();
        
#if WINDOWS
        builder.Services.AddWindowsServices();
        
        builder.ConfigureLifecycleEvents(lifecycleBuilder =>
        {
            lifecycleBuilder.AddWindows(windowsLifecycleBuilder =>
            {
                windowsLifecycleBuilder.OnWindowCreated(window =>
                {
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    var id = Win32Interop.GetWindowIdFromWindow(handle);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
                    
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
#endif
        
        // <---- App ---->

        builder
            .UseMauiApp<MauiApp>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });
        
        var app = builder.Build();
        
        _config = app.Services.GetRequiredService<ConfigManager>().Config;

        app.Services.StartShockOscServices(false);
        
        return app;
    }
}
#endif