using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.ShockOSC.Config;

namespace OpenShock.ShockOSC.Services;

public class MedalIcymiService
{
    private readonly ILogger<MedalIcymiService> _logger;
    private readonly IModuleConfig<ShockOscConfig> _moduleConfig;
    private static readonly HttpClient HttpClient = new();
    private const string BaseUrl = "http://localhost:12665/api/v1";
    // these are publicly generated and are not sensitive.
    private const string PubApiKeyVrc = "pub_x4PTxSGVk6sl8BYg5EB5qsn8QIVz4kRi";
    private const string PubApiKeyCvr = "pub_LRG3bA6XjoVSkSU4JuXmL51tJdGJWdVQ"; 

    public MedalIcymiService(ILogger<MedalIcymiService> logger, IModuleConfig<ShockOscConfig> moduleConfig)
    {
        _logger = logger;
        _moduleConfig = moduleConfig;
        switch (_moduleConfig.Config.MedalIcymi.Game)
        {
            case IcymiGame.VRChat:
                HttpClient.DefaultRequestHeaders.Add("publicKey", PubApiKeyVrc);
                break;
            case IcymiGame.ChilloutVR:
                HttpClient.DefaultRequestHeaders.Add("publicKey", PubApiKeyCvr);
                break;
            default:
                _logger.LogError("Game Selection was out of range. Value was: {value}", _moduleConfig.Config.MedalIcymi.Game);
                break;
        }
    }

    public async Task TriggerMedalIcymiAction(string eventId)
    {
        var eventPayload = new
        {
            eventId,
            eventName = _moduleConfig.Config.MedalIcymi.Name,
            
            contextTags = new
            {
                location = _moduleConfig.Config.MedalIcymi.Game.ToString(),
                description = _moduleConfig.Config.MedalIcymi.Description
            },
            triggerActions = new[]
            {
                _moduleConfig.Config.MedalIcymi.TriggerAction.ToString()
            },
            
            clipOptions = new
            {
                duration = _moduleConfig.Config.MedalIcymi.ClipDuration,
                alertType = _moduleConfig.Config.MedalIcymi.AlertType.ToString(),
            }
        };

        var jsonPayload = JsonSerializer.Serialize(eventPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await HttpClient.PostAsync($"{BaseUrl}/event/invoke", content);

            _logger.LogInformation("{triggerAction} triggered.", _moduleConfig.Config.MedalIcymi.TriggerAction);
                
            var responseContent = await response.Content.ReadAsStringAsync();
            HandleApiResponse((int)response.StatusCode, responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while creating Medal {triggerAction}: {exception}", _moduleConfig.Config.MedalIcymi.TriggerAction, ex);
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
                _logger.LogWarning("Unexpected response: {statusCode} - {response}", statusCode, responseContent);
                break;
        }
    }
}