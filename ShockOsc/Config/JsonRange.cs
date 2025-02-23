// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace OpenShock.ShockOSC.Config;

public class JsonRange<T> where T : struct
{
    public required T Min { get; set; }
    public required T Max { get; set; }
}