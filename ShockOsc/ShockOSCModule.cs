using ModuleBase;

namespace OpenShock.ShockOsc;

public sealed class ShockOSCModule : IModule
{
    public string Id => "OpenShock.ShockOsc";
    public string Name => "ShockOSC";
    public Type RootComponentType { get; }
    public string IconPath => "OpenShock.ShockOsc.Resources.ShockOSC-Icon.png";
    
    public void Start()
    {
        
    }
}