namespace OpenShock.ShockOSC.OscChangeTracker;

public interface IChangeTrackedOscParam
{
    public ValueTask Send();
}