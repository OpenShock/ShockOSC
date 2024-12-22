using System.Text;
using System.Text.Json;
using OpenShock.ShockOsc.Config;

namespace OpenShock.ShockOsc.Services;

public class MedalIcymiService
{
    private readonly ILogger<MedalIcymiService> _logger;
    private readonly ConfigManager _configManager;
    private static readonly HttpClient HttpClient = new();
    private const string BaseUrl = "http://localhost:12665/api/v1";
    // these are publicly generated and are not sensitive.
    private const string PubApiKeyVrc = "pub_x4PTxSGVk6sl8BYg5EB5qsn8QIVz4kRi";
    private const string PubApiKeyCvr = "pub_LRG3bA6XjoVSkSU4JuXmL51tJdGJWdVQ"; 

    public MedalIcymiService(ILogger<MedalIcymiService> logger, ConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
        switch (_configManager.Config.MedalIcymi.IcymiGame)
        {
            case IcymiGame.VRChat:
                HttpClient.DefaultRequestHeaders.Add("publicKey", PubApiKeyVrc);
                break;
            case IcymiGame.ChilloutVR:
                HttpClient.DefaultRequestHeaders.Add("publicKey", PubApiKeyCvr);
                break;
        }
    }

    public async Task TriggerBookmarkAsync(string eventId)
    {
        var eventPayload = new
        {
            eventId,
            eventName = _configManager.Config.MedalIcymi.IcymiName,
            
            contextTags = new
            {
                location = _configManager.Config.MedalIcymi.IcymiGame,
                description = _configManager.Config.MedalIcymi.IcymiDescription
            },
            triggerActions = new[]
            {
                _configManager.Config.MedalIcymi.IcymiTriggerAction.ToString()
            },
            
            clipOptions = new
            {
                duration = _configManager.Config.MedalIcymi.IcymiClipDuration,
                alertType = _configManager.Config.MedalIcymi.IcymiAlertType.ToString(),
            }
        };

        var jsonPayload = JsonSerializer.Serialize(eventPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await HttpClient.PostAsync($"{BaseUrl}/event/invoke", content);

            _logger.LogInformation("{0} triggered.", _configManager.Config.MedalIcymi.IcymiTriggerAction);
                
            var responseContent = await response.Content.ReadAsStringAsync();
            HandleApiResponse((int)response.StatusCode, responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while creating Medal {0}: {1}", _configManager.Config.MedalIcymi.IcymiTriggerAction, ex);
        }
    }

    private void HandleApiResponse(int statusCode, string responseContent)
    {
        switch (statusCode)
        {
            case 400 when responseContent.Contains("INVALID_MODEL"):
                _logger.LogError("Invalid model: The request body does not match the expected model structure.");
                break;

            case 400 when responseContent.Contains("INVALID_EVENT"):
                _logger.LogError("Invalid event: The provided game event details are invalid.");
                break;

            case 400 when responseContent.Contains("MISSING_PUBLIC_KEY"):
                _logger.LogError("Missing public key: The publicKey header is missing from the request.");
                break;

            case 400 when responseContent.Contains("INVALID_APP_DATA"):
                _logger.LogError("Invalid app data: Failed to retrieve app data associated with the provided public key.");
                break;

            case 200 when responseContent.Contains("INACTIVE_GAME"):
                _logger.LogWarning("Inactive game: The event was received but not processed because the categoryId does not match the active game.");
                break;

            case 200 when responseContent.Contains("DISABLED_EVENT"):
                _logger.LogWarning("Disabled event: The event was received but not processed because it is disabled in the user’s ICYMI settings.");
                break;

            case 200 when responseContent.Contains("success"):
                _logger.LogDebug("Event received and processed successfully");
                break;
            
            case 500:
                _logger.LogError("Internal server error: An unexpected error occurred while processing the request.");
                break;

            default:
                _logger.LogWarning("Unexpected response: {0} - {1}", statusCode, responseContent);
                break;
        }
    }
}