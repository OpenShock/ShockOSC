using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Resources;
using System.Runtime.InteropServices;

namespace OpenShock.Desktop.Plugin;

public abstract class PluginBase
{
    public abstract string Name { get; }
    public abstract IComponent WebRoot { get; }
    public abstract ResourceManager WebResourceManager { get; }
    public abstract IEnumerable<OSPlatform> SupportedPlatforms { get; }

    public void RegisterServices(IServiceCollection services) { }
    public void StartServices(IServiceProvider serviceProvider) { }
}
