using Microsoft.Extensions.Hosting;

namespace OpenShock.ShockOsc;

public static class HeadlessProgram
{
    public static IHost SetupHeadlessHost()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddShockOscServices();

#if WINDOWS
            services.AddWindowsServices();
#endif
        });
        
        var app = builder.Build();
        app.Services.StartShockOscServices(true);
        
        return app;
    }
}