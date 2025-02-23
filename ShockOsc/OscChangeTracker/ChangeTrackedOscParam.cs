using OpenShock.ShockOSC.Services;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OpenShock.ShockOSC.OscChangeTracker;

public class ChangeTrackedOscParam<T> : IChangeTrackedOscParam
{
    private readonly OscClient _oscClient;

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ILogger Logger = Log.ForContext(typeof(ChangeTrackedOscParam<>));
    
    public string Address { get; }
    public T Value { get; private set; }

    public ChangeTrackedOscParam(string address, T initialValue, OscClient oscClient)
    {
        _oscClient = oscClient;
        Address = address;
        Value = initialValue;
    }

    public ChangeTrackedOscParam(string shockerName, string suffix, T initialValue, OscClient oscClient) : this(
        $"/avatar/parameters/ShockOsc/{shockerName}{suffix}", initialValue, oscClient)
    {
    }

    public ValueTask Send()
    {
        Logger.Debug("Sending parameter update for [{ParameterAddress}] with value [{Value}]", Address, Value);
        return _oscClient.SendGameMessage(Address, Value);  
    } 

    public ValueTask SetValue(T value)
    {
        if (Value!.Equals(value)) return ValueTask.CompletedTask;
        Value = value;
        return Send();
    }
}