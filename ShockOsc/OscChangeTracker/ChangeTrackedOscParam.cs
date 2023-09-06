using Serilog;

namespace ShockLink.ShockOsc.OscChangeTracker;

public class ChangeTrackedOscParam<T> : IChangeTrackedOscParam
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly ILogger Logger = Log.ForContext(typeof(ChangeTrackedOscParam<>));
    
    public string Address { get; }
    public T Value { get; private set; }

    public ChangeTrackedOscParam(string address, T initialValue)
    {
        Address = address;
        Value = initialValue;
    }

    public ChangeTrackedOscParam(string shockerName, string suffix, T initialValue) : this(
        $"/avatar/parameters/ShockOsc/{shockerName}{suffix}", initialValue)
    {
    }

    public ValueTask Send()
    {
        Logger.Debug("Sending parameter update for [{ParameterAddress}] with value [{Value}]", Address, Value);
        return OscClient.SendGameMessage(Address, Value);  
    } 

    public ValueTask SetValue(T value)
    {
        if (Value!.Equals(value)) return ValueTask.CompletedTask;
        Value = value;
        return Send();
    }
}