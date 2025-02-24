using System.Net;
using MudBlazor;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.ModuleBase.Navigation;
using OpenShock.ShockOSC.Config;
using OpenShock.ShockOSC.Services;
using OpenShock.ShockOSC.Ui.Pages.Dash.Tabs;
using OscQueryLibrary;

namespace OpenShock.ShockOSC;

public sealed class ShockOSCModule : DesktopModuleBase
{
    public override string Id => "OpenShock.ShockOSC";
    public override string Name => "ShockOSC";
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
    }
}