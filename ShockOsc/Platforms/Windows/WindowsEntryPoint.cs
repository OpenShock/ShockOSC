#if WINDOWS
using System.Runtime.InteropServices;
using CommandLine;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using OpenShock.ShockOsc.Cli;
using OpenShock.ShockOsc.Services;
using OpenShock.ShockOsc.Utils;
using WinRT;
using Application = Microsoft.UI.Xaml.Application;

namespace OpenShock.ShockOsc.Platforms.Windows;

public static class WindowsEntryPoint
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    [STAThread]
    private static void Main(string[] args)
    {
        var parsed = Parser.Default.ParseArguments<CliOptions>(args);
        parsed.WithParsed(Start);
        parsed.WithNotParsed(errors =>
        {
            errors.Output();
            Environment.Exit(1);
        });
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
        
        XamlCheckProcessRequirements();
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(delegate
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            // ReSharper disable once ObjectCreationAsStatement
            new App();
        });
    }
}
#endif