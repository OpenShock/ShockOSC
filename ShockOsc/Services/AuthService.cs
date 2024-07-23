using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Config;

namespace OpenShock.ShockOsc.Services;

public sealed class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly BackendHubManager _backendHubManager;
    private readonly OpenShockHubClient _hubClient;
    private readonly LiveControlManager _liveControlManager;
    private readonly OpenShockApi _apiClient;
    private readonly ConfigManager _configManager;

    public bool Authenticated { get; private set; }

    public AuthService(ILogger<AuthService> logger,
        BackendHubManager backendHubManager,
        OpenShockHubClient hubClient,
        LiveControlManager liveControlManager,
        OpenShockApi apiClient,
        ConfigManager configManager)
    {
        _logger = logger;
        _backendHubManager = backendHubManager;
        _hubClient = hubClient;
        _liveControlManager = liveControlManager;
        _apiClient = apiClient;
        _configManager = configManager;
    }

    private SemaphoreSlim _authLock = new(1, 1);
    
    public async Task Authenticate()
    {
        await _authLock.WaitAsync();
        try
        {
            if (Authenticated) return;
            Authenticated = false;

            _logger.LogInformation("Setting up api client");
            _apiClient.SetupApiClient();
            _logger.LogInformation("Setting up live client");
            await _backendHubManager.SetupLiveClient();
            _logger.LogInformation("Starting live client");
            await _hubClient.StartAsync();

            _logger.LogInformation("Refreshing shockers");
            await _apiClient.RefreshShockers();

            await _liveControlManager.RefreshConnections();

            Authenticated = true;
        }
        finally
        {
            _authLock.Release();
        }
    }

    public async Task Logout()
    {
        Authenticated = false;
        
        _configManager.Config.OpenShock.Token = string.Empty;
        _configManager.Save();
        
        _logger.LogInformation("Logging out");
        await _hubClient.StopAsync();
        
        _apiClient.Logout();

        await _liveControlManager.RefreshConnections();
        
        _logger.LogInformation("Logged out");
    }
}