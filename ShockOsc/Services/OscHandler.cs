using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.ShockOSC.Config;
using OpenShock.ShockOSC.OscChangeTracker;
using OpenShock.Internal.Common.Utils;

namespace OpenShock.ShockOSC.Services;

public sealed class OscHandler
{
    private readonly ChangeTrackedOscParam<bool> _paramAnyActive;
    private readonly ChangeTrackedOscParam<bool> _paramAnyCooldown;
    private readonly ChangeTrackedOscParam<float> _paramAnyCooldownPercentage;
    private readonly ChangeTrackedOscParam<float> _paramAnyIntensity;

    private readonly ConcurrentDictionary<Guid, LastControlLogEntry> _lastControlLogs = new();

    private readonly ILogger<OscHandler> _logger;
    private readonly OscClient _oscClient;
    private readonly IModuleConfig<ShockOscConfig> _moduleConfig;
    private readonly ShockOscData _shockOscData;

    public OscHandler(ILogger<OscHandler> logger, OscClient oscClient, IModuleConfig<ShockOscConfig> moduleConfig,
        ShockOscData shockOscData)
    {
        _logger = logger;
        _oscClient = oscClient;
        _moduleConfig = moduleConfig;
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
        if (!_moduleConfig.Config.Behaviour.ForceUnmute || !_shockOscData.IsMuted) return;

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

        await UpdateAnyActive();

        var anyCooldown = false;
        var anyCooldownPercentage = 0f;

        foreach (var shocker in _shockOscData.ProgramGroups.Values)
        {
            var cooldownTime = _moduleConfig.Config.Behaviour.CooldownTime;
            if (shocker.ConfigGroup is { OverrideCooldownTime: true })
                cooldownTime = shocker.ConfigGroup.CooldownTime;

            var isActive = shocker.LastExecuted.AddMilliseconds(shocker.LastDuration) > DateTime.UtcNow;
            var isActiveOrOnCooldown =
                shocker.LastExecuted.AddMilliseconds(cooldownTime)
                    .AddMilliseconds(shocker.LastDuration) > DateTime.UtcNow;
            if (!isActiveOrOnCooldown && shocker.LastIntensity > 0)
                shocker.LastIntensity = 0;

            var intensity = MathUtils.Saturate(shocker.LastIntensity / 100f);
            var onCoolDown = !isActive && isActiveOrOnCooldown;
            var cooldownPercentage = 0f;
            if (onCoolDown)
                cooldownPercentage = MathUtils.Saturate(1 -
                                                        (float)(DateTime.UtcNow -
                                                                shocker.LastExecuted.AddMilliseconds(
                                                                    shocker.LastDuration))
                                                        .TotalMilliseconds /
                                                        cooldownTime);

            await shocker.ParamActive.SetValue(isActive);
            await shocker.ParamCooldown.SetValue(onCoolDown);
            await shocker.ParamCooldownPercentage.SetValue(cooldownPercentage);
            await shocker.ParamIntensity.SetValue(intensity);

            if (onCoolDown) anyCooldown = true;
            anyCooldownPercentage = MathF.Max(anyCooldownPercentage, cooldownPercentage);
        }

        await _paramAnyCooldown.SetValue(anyCooldown);
        await _paramAnyCooldownPercentage.SetValue(anyCooldownPercentage);
    }

    private async Task UpdateAnyActive()
    {
        var now = DateTimeOffset.UtcNow;
        var anyActive = false;
        var maxIntensity = 0f;

        foreach (var logEntry in _lastControlLogs.Values)
        {
            if (logEntry.ControlLog.Type != ControlType.Shock) continue;
            var activeUntil = logEntry.Timestamp.AddMilliseconds(logEntry.ControlLog.Duration);

            if (activeUntil < now) continue;

            anyActive = true;
            var intensity = MathUtils.Saturate(logEntry.ControlLog.Intensity / 100f);
            maxIntensity = MathF.Max(maxIntensity, intensity);
        }

        await _paramAnyActive.SetValue(anyActive);
        await _paramAnyIntensity.SetValue(maxIntensity);
    }

    public void SetLastControlCommand(LastControlLogEntry controlLogs)
    {
        _lastControlLogs[controlLogs.ControlLog.Shocker.Id] = controlLogs;
    }

    public class LastControlLogEntry
    {
        public required DateTimeOffset Timestamp { get; set; }
        public required ControlLog ControlLog { get; set; }
    }
}