using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenShock.ShockOsc.Cli;
using OpenShock.ShockOsc.Services;
using OpenShock.ShockOsc.Utils;

#if CROSS
namespace OpenShock.ShockOsc.Platforms.Cross;

public static class CrossEntryPoint
{
    public static Task Main(string[] args)
    {
        return ParseHelper.ParseAsync(args, Start);
    }

    private static async Task Start(CliOptions config)
    {
        if (config.Headless)
        {
            Console.WriteLine("Running in headless mode.");

            var host = HeadlessProgram.SetupHeadlessHost();
            OsTask.Run(host.Services.GetRequiredService<AuthService>().Authenticate);
            await host.RunAsync();

            return;
        }
        
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddShockOscServices();
            services.AddCommonBlazorServices();
            
            // Setup blazor server side

            
#if WINDOWS
            services.AddWindowsServices();
#endif
        });
        
        
        var app = builder.Build();
        app.Services.StartShockOscServices(true);
        
        await app.RunAsync();
    }
}

#endif