namespace OpenShock.ShockOsc.OscChangeTracker;

public interface IChangeTrackedOscParam
{
    public ValueTask Send();
}