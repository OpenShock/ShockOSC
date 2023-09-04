namespace ShockLink.ShockOsc.OscChangeTracker;

public interface IChangeTrackedOscParam
{
    public ValueTask Send();
}