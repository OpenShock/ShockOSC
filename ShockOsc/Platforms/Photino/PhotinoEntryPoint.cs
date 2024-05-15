#if PHOTINO
using OpenShock.ShockOsc.Cli;
using OpenShock.ShockOsc.Services;
using OpenShock.ShockOsc.Ui;
using OpenShock.ShockOsc.Utils;
using Photino.Blazor;

namespace OpenShock.ShockOsc.Platforms.Photino;

public static class PhotinoEntryPoint
{
    [STAThread]
    public static void Main(string[] args)
    {
        ParseHelper.Parse<CliOptions>(args, Start);
    }

    private static void Start(CliOptions config)
    {
        if (config.Headless)
        {
            Console.WriteLine("Running in headless mode.");

            var host = HeadlessProgram.SetupHeadlessHost();
            OsTask.Run(host.Services.GetRequiredService<AuthService>().Authenticate);
            host.Run();

            return;
        }

        var builder = PhotinoBlazorAppBuilder.CreateDefault();
        builder.Services.AddShockOscServices();
        builder.Services.AddCommonBlazorServices();
        
        builder.Services.Configure((Action<PhotinoBlazorAppConfiguration>) (opts =>
        {
            opts.HostPage = "photino.html";
        }));
        
        builder.RootComponents.Add<Main>("#app");

        var app = builder.Build();

        app.MainWindow
            .SetIconFile("Resources/Icon512.png")
            .SetTitle("ShockOSC");
        
        app.MainWindow.MinHeight = 600;
        app.MainWindow.MinWidth = 1000;
        
        app.Services.StartShockOscServices(true);
        
        app.Run();
    }
}
#endif