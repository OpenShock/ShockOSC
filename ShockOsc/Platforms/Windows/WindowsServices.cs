#if WINDOWS
using OpenShock.ShockOsc.Services;

namespace OpenShock.ShockOsc;

public static class WindowsServices
{
    public static void AddWindowsServices(this IServiceCollection services)
    {
        services.AddSingleton<ITrayService, WindowsTrayService>();
    }
}
#endif