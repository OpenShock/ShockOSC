using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Ui.Components;

namespace OpenShock.ShockOsc.Services;

public sealed class BackendControlService
{
    private readonly BackendHubManager _backendHubManager;

    public BackendControlService(BackendHubManager backendHubManager)
    {
        _backendHubManager = backendHubManager;
    }
}