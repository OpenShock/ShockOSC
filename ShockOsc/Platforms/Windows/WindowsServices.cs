#if WINDOWS
using OpenShock.ShockOsc.Services;

// ReSharper disable once CheckNamespace
namespace OpenShock.ShockOsc.Platforms.Windows;

public static class WindowsServices
{
    public static void AddWindowsServices(this IServiceCollection services)
    {
        services.AddSingleton<ITrayService, WindowsTrayService>();
    }
}
#endif