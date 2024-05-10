using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Hub.Models;
using OpenShock.SDK.CSharp.Live;
using OpenShock.SDK.CSharp.Live.LiveControlModels;
using OpenShock.SDK.CSharp.Models;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Models;
using OpenShock.ShockOsc.Services;
using Serilog;
using SmartFormat;

namespace OpenShock.ShockOsc.Backend;

public sealed class BackendHubManager
{
    private readonly ILogger<BackendHubManager> _logger;
    private readonly ConfigManager _configManager;
    private readonly OpenShockHubClient _openShockHubClient;
    private readonly OscClient _oscClient;
    private readonly ShockOscData _dataLayer;
    private readonly OscHandler _oscHandler;

    private string _liveConnectionId = string.Empty;

    public BackendHubManager(ILogger<BackendHubManager> logger,
        ConfigManager configManager,
        OpenShockHubClient openShockHubClient,
        OscClient oscClient,
        ShockOscData dataLayer,
        OscHandler oscHandler)
    {
        _logger = logger;
        _configManager = configManager;
        _openShockHubClient = openShockHubClient;
        _oscClient = oscClient;
        _dataLayer = dataLayer;
        _oscHandler = oscHandler;

        _openShockHubClient.OnWelcome += s =>
        {
            _liveConnectionId = s;
            return Task.CompletedTask;
        };

        _openShockHubClient.OnLog += RemoteActivateShockers;
    }


    public async Task SetupLiveClient()
    {
        await _openShockHubClient.Setup(new HubClientOptions()
        {
            Token = _configManager.Config.OpenShock.Token,
            Server = _configManager.Config.OpenShock.Backend,
            ConfigureLogging = builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddSerilog();
            }
        });
    }

    /// <summary>
    /// Send a stop command for every shocker in a group
    /// </summary>
    /// <param name="programGroup"></param>
    /// <returns></returns>
    public Task<bool> CancelControl(ProgramGroup programGroup)
    {
        _logger.LogTrace("Cancelling action");
        return ControlGroup(programGroup.Id, 0, 0, ControlType.Stop);
    }

    /// <summary>
    /// Control a group, if guid is empty, all shockers will be controlled
    /// </summary>
    /// <param name="groupId"></param>
    /// <param name="duration"></param>
    /// <param name="intensity"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public async Task<bool> ControlGroup(Guid groupId, uint duration, byte intensity, ControlType type, bool exclusive = false)
    {
        if (groupId == Guid.Empty)
        {
            var controlCommandsAll = _configManager.Config.OpenShock.Shockers
                .Where(x => x.Value.Enabled)
                .Select(x => new Control
                {
                    Id = x.Key,
                    Duration = duration,
                    Intensity = intensity,
                    Type = type,
                    Exclusive = exclusive
                });
            await _openShockHubClient.Control(controlCommandsAll);
            return true;
        }


        if (!_configManager.Config.Groups.TryGetValue(groupId, out var group)) return false;

        var controlCommands = group.Shockers.Select(x => new Control
        {
            Id = x,
            Duration = duration,
            Intensity = intensity,
            Type = type,
            Exclusive = exclusive
        });

        await _openShockHubClient.Control(controlCommands);
        return true;
    }

    private async Task RemoteActivateShockers(ControlLogSender sender, ICollection<ControlLog> logs)
    {
        if (sender.ConnectionId == _liveConnectionId)
        {
            _logger.LogDebug("Ignoring remote command log cause it was the local connection");
            return;
        }

        foreach (var controlLog in logs) await RemoteActivateShocker(sender, controlLog);
    }

    private async Task RemoteActivateShocker(ControlLogSender sender, ControlLog log)
    {
        var inSeconds = ((float)log.Duration / 1000).ToString(CultureInfo.InvariantCulture);

        if (sender.CustomName == null)
            _logger.LogInformation(
                "Received remote {Type} for \"{ShockerName}\" at {Intensity}%:{Duration}s by {Sender}",
                log.Type, log.Shocker.Name, log.Intensity, inSeconds, sender.Name);
        else
            _logger.LogInformation(
                "Received remote {Type} for \"{ShockerName}\" at {Intensity}%:{Duration}s by {SenderCustomName} [{Sender}]",
                log.Type, log.Shocker.Name, log.Intensity, inSeconds, sender.CustomName, sender.Name);

        var template = _configManager.Config.Chatbox.Types[log.Type];
        if (_configManager.Config.Chatbox.Enabled &&
            _configManager.Config.Chatbox.DisplayRemoteControl && template.Enabled)
        {
            // Chatbox message remote
            var dat = new
            {
                ShockerName = log.Shocker.Name,
                Intensity = log.Intensity,
                Duration = log.Duration,
                DurationSeconds = inSeconds,
                Name = sender.Name,
                CustomName = sender.CustomName
            };

            var msg =
                $"{_configManager.Config.Chatbox.Prefix}{Smart.Format(sender.CustomName == null ? template.Remote : template.RemoteWithCustomName, dat)}";
            await _oscClient.SendChatboxMessage(msg);
        }

        var configGroupsAffected = _configManager.Config.Groups
            .Where(s => s.Value.Shockers.Any(x => x == log.Shocker.Id)).Select(x => x.Key).ToArray();
        var programGroupsAffected = _dataLayer.ProgramGroups.Where(x => configGroupsAffected.Contains(x.Key))
            .Select(x => x.Value);
        var oneShock = false;

        foreach (var pain in programGroupsAffected)
        {
            switch (log.Type)

            {
                case ControlType.Shock:
                {
                    pain.LastIntensity = log.Intensity;
                    pain.LastDuration = log.Duration;
                    pain.LastExecuted = log.ExecutedAt;

                    oneShock = true;
                    break;
                }
                case ControlType.Vibrate:
                    pain.LastVibration = log.ExecutedAt;
                    break;
                case ControlType.Stop:
                    pain.LastDuration = 0;
                    await _oscHandler.SendParams();
                    break;
                case ControlType.Sound:
                    break;
                default:
                    _logger.LogError("ControlType was out of range. Value was: {Type}", log.Type);
                    break;
            }

            if (oneShock)
            {
                await _oscHandler.ForceUnmute();
                await _oscHandler.SendParams();
            }
        }
    }
}