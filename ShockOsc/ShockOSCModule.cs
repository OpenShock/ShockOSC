using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.ModuleBase.Navigation;
using OpenShock.ShockOSC;
using OpenShock.ShockOSC.Config;
using OpenShock.ShockOSC.Services;
using OpenShock.ShockOSC.Ui.Pages.Dash.Tabs;
using OscQueryLibrary;
// ReSharper disable InconsistentNaming

[assembly:DesktopModule(typeof(ShockOSCModule), "openshock.shockosc", "ShockOSC")]
namespace OpenShock.ShockOSC;

public sealed class ShockOSCModule : DesktopModuleBase, IAsyncDisposable
{
    private IAsyncDisposable? _onRemoteControlSubscription;
    public override string IconPath => "OpenShock/ShockOSC/Resources/ShockOSC-Icon.svg";

    public override IReadOnlyCollection<NavigationItem> NavigationComponents { get; } =
    [
        new()
        {
            Name = "Settings",
            ComponentType = typeof(ConfigTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.Settings)
        },
        new()
        {
            Name = "Groups",
            ComponentType = typeof(GroupsTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.Group)
        },
        new()
        {
            Name = "Chatbox",
            ComponentType = typeof(ChatboxTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.Chat)
        },
        new()
        {
            Name = "Debug",
            ComponentType = typeof(DebugTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.BugReport)
        }
    ];

    public override async Task Setup()
    {

        var config = await ModuleInstanceManager.GetModuleConfig<ShockOscConfig>();
        ModuleServiceProvider = BuildServices(config);
        
    }

    private IServiceProvider BuildServices(IModuleConfig<ShockOscConfig> config)
    {
        var loggerFactory = ModuleInstanceManager.AppServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var services = new ServiceCollection();

        services.AddSingleton(loggerFactory);
        services.AddLogging();
        services.AddSingleton(config);

        services.AddSingleton<IOpenShockService>(ModuleInstanceManager.OpenShock);
        services.AddSingleton<ShockOscData>();
        services.AddSingleton<OscClient>();
        services.AddSingleton<OscHandler>();
        services.AddSingleton<ChatboxService>();
        
        services.AddSingleton(provider =>
        {
            var listenAddress = config.Config.Osc.QuestSupport ? IPAddress.Any : IPAddress.Loopback;
            return new OscQueryServer("ShockOsc", listenAddress);
        });
        
        services.AddSingleton<ShockOsc>();
        services.AddSingleton<UnderscoreConfig>();
        services.AddSingleton<MedalIcymiService>();
        
        
        return services.BuildServiceProvider();
    }        

    public override async Task Start()
    {
        var config = ModuleServiceProvider.GetRequiredService<IModuleConfig<ShockOscConfig>>();

        await ModuleServiceProvider.GetRequiredService<Services.ShockOsc>().Start();
        
        if (config.Config.Osc.OscQuery) ModuleServiceProvider.GetRequiredService<OscQueryServer>().Start();
        
        var chatboxService = ModuleServiceProvider.GetRequiredService<ChatboxService>();

        _onRemoteControlSubscription = await ModuleInstanceManager.OpenShock.Control.OnRemoteControlledShocker.SubscribeAsync(async args =>
        {
            foreach (var controlLog in args.Logs)
            {
                await chatboxService.SendRemoteControlMessage(controlLog.Shocker.Name, args.Sender.Name,
                    args.Sender.CustomName, controlLog.Intensity, controlLog.Duration, controlLog.Type);
            }
        });
    }
    
    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if(_disposed) return;
        _disposed = true;

        if (_onRemoteControlSubscription != null) await _onRemoteControlSubscription.DisposeAsync();
    }
}