using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.ShockOsc.Backend;

namespace OpenShock.ShockOsc.Services;

public sealed class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly BackendHubManager _backendHubManager;
    private readonly OpenShockHubClient _hubClient;
    private readonly LiveControlManager _liveControlManager;
    private readonly OpenShockApi _apiClient;

    public AuthService(ILogger<AuthService> logger, BackendHubManager backendHubManager, OpenShockHubClient hubClient, LiveControlManager liveControlManager, OpenShockApi apiClient)
    {
        _logger = logger;
        _backendHubManager = backendHubManager;
        _hubClient = hubClient;
        _liveControlManager = liveControlManager;
        _apiClient = apiClient;
    }

    public async Task Authenticate()
    {
        _logger.LogInformation("Setting up api client");
        _apiClient.SetupApiClient();
        _logger.LogInformation("Setting up live client");
        await _backendHubManager.SetupLiveClient();
        _logger.LogInformation("Starting live client");
        await _hubClient.StartAsync();

        _logger.LogInformation("Refreshing shockers");
        await _apiClient.RefreshShockers();

        await _liveControlManager.RefreshConnections();
    }
}