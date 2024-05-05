using Microsoft.Extensions.Logging;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.OscChangeTracker;
using OpenShock.ShockOsc.Utils;

namespace OpenShock.ShockOsc.Services;

public sealed class OscHandler
{
    private readonly ChangeTrackedOscParam<bool> _paramAnyActive;
    private readonly ChangeTrackedOscParam<bool> _paramAnyCooldown;
    private readonly ChangeTrackedOscParam<float> _paramAnyCooldownPercentage;
    private readonly ChangeTrackedOscParam<float> _paramAnyIntensity;
    
    private readonly ILogger<OscHandler> _logger;
    private readonly OscClient _oscClient;
    private readonly ConfigManager _configManager;
    private readonly ShockOscData _shockOscData;
    
    public OscHandler(ILogger<OscHandler> logger, OscClient oscClient, ConfigManager configManager, ShockOscData shockOscData)
    {
        _logger = logger;
        _oscClient = oscClient;
        _configManager = configManager;
        _shockOscData = shockOscData;
        
        _paramAnyActive = new ChangeTrackedOscParam<bool>("_Any", "_Active", false, _oscClient);
        _paramAnyCooldown = new ChangeTrackedOscParam<bool>("_Any", "_Cooldown", false, _oscClient);
        _paramAnyCooldownPercentage = new ChangeTrackedOscParam<float>("_Any", "_CooldownPercentage", 0f, _oscClient);
        _paramAnyIntensity = new ChangeTrackedOscParam<float>("_Any", "_Intensity", 0f, _oscClient);
    }
    
    /// <summary>
    /// Force unmute the users if enabled in config
    /// </summary>
    public async Task ForceUnmute()
    {
        // If we don't have to force unmute or we're not muted, also check config here.
        if (!_configManager.Config.Behaviour.ForceUnmute || !_shockOscData.IsMuted) return;
        
        _logger.LogDebug("Force unmuting...");
        
        // So this is absolutely disgusting, but vrchat seems to be very retarded.
        // PS: If you send true for more than 500ms the game locks up.
        
        // Button press off
        await _oscClient.SendGameMessage("/input/Voice", false)
            .ConfigureAwait(false);
        
        // We wait 50 ms..
        await Task.Delay(50)
            .ConfigureAwait(false);
        
        // Button press on
        await _oscClient.SendGameMessage("/input/Voice", true)
            .ConfigureAwait(false);
        
        // We wait 50 ms..
        await Task.Delay(50)
            .ConfigureAwait(false);
        
        // Button press off
        await _oscClient.SendGameMessage("/input/Voice", false)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Send parameter updates to osc
    /// </summary>
    public async Task SendParams()
    {
        // TODO: maybe force resend on avatar change
        var anyActive = false;
        var anyCooldown = false;
        var anyCooldownPercentage = 0f;
        var anyIntensity = 0f;

        foreach (var shocker in _shockOscData.ProgramGroups.Values)
        {
            var isActive = shocker.LastExecuted.AddMilliseconds(shocker.LastDuration) > DateTime.UtcNow;
            var isActiveOrOnCooldown =
                shocker.LastExecuted.AddMilliseconds(_configManager.Config.Behaviour.CooldownTime)
                    .AddMilliseconds(shocker.LastDuration) > DateTime.UtcNow;
            if (!isActiveOrOnCooldown && shocker.LastIntensity > 0)
                shocker.LastIntensity = 0;

            var intensity = MathUtils.ClampFloat(shocker.LastIntensity / 100f);
            var onCoolDown = !isActive && isActiveOrOnCooldown;
            var cooldownPercentage = 0f;
            if (onCoolDown)
                cooldownPercentage = MathUtils.ClampFloat(1 -
                                                          (float)(DateTime.UtcNow -
                                                                  shocker.LastExecuted.AddMilliseconds(shocker.LastDuration))
                                                          .TotalMilliseconds /
                                                          _configManager.Config.Behaviour.CooldownTime);

            await shocker.ParamActive.SetValue(isActive);
            await shocker.ParamCooldown.SetValue(onCoolDown);
            await shocker.ParamCooldownPercentage.SetValue(cooldownPercentage);
            await shocker.ParamIntensity.SetValue(intensity);

            if (isActive) anyActive = true;
            if (onCoolDown) anyCooldown = true;
            anyCooldownPercentage = MathF.Max(anyCooldownPercentage, cooldownPercentage);
            anyIntensity = MathF.Max(anyIntensity, intensity);
        }

        await _paramAnyActive.SetValue(anyActive);
        await _paramAnyCooldown.SetValue(anyCooldown);
        await _paramAnyCooldownPercentage.SetValue(anyCooldownPercentage);
        await _paramAnyIntensity.SetValue(anyIntensity);
    }
}