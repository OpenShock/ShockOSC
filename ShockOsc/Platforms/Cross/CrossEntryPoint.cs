#if CROSS
using OpenShock.ShockOsc.Cli;
using OpenShock.ShockOsc.Services;
using OpenShock.ShockOsc.Utils;

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
        
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        builder.Services.AddShockOscServices();
        builder.Services.AddCommonBlazorServices();
        
#if WINDOWS
            builder.Services.AddWindowsServices();
#endif

        var app = builder.Build();
        
        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        
        app.Services.StartShockOscServices(true);
        
        OsTask.Run(app.Services.GetRequiredService<AuthService>().Authenticate);
        
        await app.RunAsync();
    }
}

#endif