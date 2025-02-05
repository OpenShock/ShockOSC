using MudBlazor;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Navigation;
using OpenShock.ShockOsc.Ui.Pages.Dash.Tabs;

namespace OpenShock.ShockOsc;

public sealed class ShockOSCModule : DesktopModuleBase
{
    public override string Id => "OpenShock.ShockOsc";
    public override string Name => "ShockOSC";
    public override string IconPath => "OpenShock.ShockOsc.Resources.ShockOSC-Icon.png";

    public override IReadOnlyCollection<NavigationItem> NavigationComponents =>
    [
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
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.ChatBubble)
        },
        new()
        {
            Name = "Debug",
            ComponentType = typeof(DebugTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.BugReport)
        },
        new()
        {
            Name = "Config",
            ComponentType = typeof(ConfigTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.Settings)
        }
    ];
}