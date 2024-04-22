using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Utils;

namespace OpenShock.ShockOsc.Services;

public sealed class StatusHandler : IDisposable
{
    private readonly OpenShockHubClient _hubClient;
    private readonly LiveControlManager _liveControlManager;
    private readonly ILogger<StatusHandler> _logger;

    public StatusHandler(OpenShockHubClient hubClient, LiveControlManager liveControlManager, ILogger<StatusHandler> logger)
    {
        _hubClient = hubClient;
        _liveControlManager = liveControlManager;
        _logger = logger;
        
        _hubClient.Reconnecting += ApiHubClientOnReconnecting;
        _hubClient.Reconnected += ApiHubClientOnReconnected;
        _hubClient.Closed += ApiHubClientOnClosed;
        _hubClient.Connected += AbiHubClientOnConnected;

        _liveControlManager.OnStateUpdated += LiveControlManagerOnOnStateUpdated;
    }
    
    public event Func<Task>? OnWebsocketStatusChanged;
    
    private async Task AbiHubClientOnConnected(string? arg)
    {
        if(OnWebsocketStatusChanged != null) await OnWebsocketStatusChanged.Raise();
        _logger.LogDebug("Connected to hub");
    }

    private async Task LiveControlManagerOnOnStateUpdated()
    {
       if(OnWebsocketStatusChanged != null) await OnWebsocketStatusChanged.Raise();
    }

    private async Task ApiHubClientOnReconnected(string? arg)
    {
        if(OnWebsocketStatusChanged != null) await OnWebsocketStatusChanged.Raise();
        _logger.LogDebug("Reconnected to hub");
    }


    private async Task ApiHubClientOnReconnecting(Exception? arg)
    {
        if(OnWebsocketStatusChanged != null) await OnWebsocketStatusChanged.Raise();
        _logger.LogDebug("Reconnecting to hub...");
    }

    private async Task ApiHubClientOnClosed(Exception? arg)
    {
        if(OnWebsocketStatusChanged != null) await OnWebsocketStatusChanged.Raise();
        _logger.LogDebug("Disconnected from hub");
    }
    
    public void Dispose()
    {
        _hubClient.Reconnecting -= ApiHubClientOnReconnecting;
        _hubClient.Reconnected -= ApiHubClientOnReconnected;
        _hubClient.Closed -= ApiHubClientOnClosed;

        _liveControlManager.OnStateUpdated -= LiveControlManagerOnOnStateUpdated;
    }
}