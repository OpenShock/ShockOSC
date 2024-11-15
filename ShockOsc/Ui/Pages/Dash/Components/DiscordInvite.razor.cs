using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OpenShock.ShockOsc.Utils;

namespace OpenShock.ShockOsc.Ui.Pages.Dash.Components;

public partial class DiscordInvite : ComponentBase
{
    [Inject]
    private IMemoryCache MemoryCache { get; init; } = null!;
    
    [Inject]
    private ILogger<DiscordInvite> Logger { get; init; } = null!;

    private static HttpClient _httpClient = new HttpClient();

    static DiscordInvite()
    {
        _httpClient.BaseAddress = new Uri("https://discord.com/api/v10/");
    }
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public required string InviteCode { get; set; }

    private DiscordInviteResponse? _invite;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _invite = await GetInvite();
        } catch (Exception e)
        {
            Logger.LogError(e, "Failed to load discord invite {InviteCode}", InviteCode);
        }

        _loading = false;
    }

    public void JoinDiscord()
    {
        UiUtils.OpenUrl($"https://discord.gg/{InviteCode}");
    }

    private async Task<DiscordInviteResponse?> GetInvite()
    {
        Logger.LogDebug("Loading discord invite {InviteCode}", InviteCode);
#pragma warning disable CS8600
        if (MemoryCache.TryGetValue($"discord_invite_{InviteCode}", out DiscordInviteResponse cachedInvite))
        {
            Logger.LogDebug("Returning cached invite {InviteCode}", InviteCode);
            return cachedInvite;
        }
#pragma warning restore CS8600
            
        Logger.LogDebug("Fetching invite {InviteCode}", InviteCode);
        var response = await _httpClient.GetAsync($"invites/{InviteCode}?with_counts=true");
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to fetch invite {InviteCode}. API returned {StatusCode}", InviteCode, response.StatusCode);
            return null;
        }

        var invite = await response.Content.ReadFromJsonAsync<DiscordInviteResponse>();
        
        MemoryCache.Set($"discord_invite_{InviteCode}", invite, TimeSpan.FromMinutes(5));
        
        Logger.LogDebug("Fetched invite {InviteCode}", InviteCode);
        
        return invite;
    }
}

public sealed class DiscordInviteResponse
{
    [JsonPropertyName("approximate_member_count")]
    public required uint ApproximateMemberCount { get; set; }
    
    [JsonPropertyName("approximate_presence_count")]
    public required uint ApproximatePresenceCount { get; set; }
    
    [JsonPropertyName("guild")]
    public required DiscordGuild Guild { get; set; }
}

public sealed class DiscordGuild
{
    [JsonPropertyName("id")]
    public required ulong Id { get; set; }
    
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("icon")]
    public required string Icon { get; set; }
    
    [JsonPropertyName("splash")]
    public required string Splash { get; set; }
    
    [JsonPropertyName("banner")]
    public required string Banner { get; set; }
}