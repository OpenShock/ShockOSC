using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Ui.Components;

namespace OpenShock.ShockOsc.Services;

public sealed class BackendControlService
{
    private readonly BackendLiveApiManager _backendLiveApiManager;

    public BackendControlService(BackendLiveApiManager backendLiveApiManager)
    {
        _backendLiveApiManager = backendLiveApiManager;
    }
}