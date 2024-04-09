using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using LucHeart.CoreOSC;
using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Live;
using OpenShock.SDK.CSharp.Live.Models;
using OpenShock.SDK.CSharp.Models;
using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Models;
using OpenShock.ShockOsc.OscChangeTracker;
using OpenShock.ShockOsc.OscQueryLibrary;
using OpenShock.ShockOsc.Utils;
using SmartFormat;

#pragma warning disable CS4014

namespace OpenShock.ShockOsc;

public sealed class ShockOsc
{
    private readonly ILogger<ShockOsc> _logger;
    private readonly OscClient _oscClient;
    private readonly OpenShockApiLiveClient _liveClient;
    private readonly UnderscoreConfig _underscoreConfig;
    private readonly ConfigManager _configManager;
    private readonly OscQueryServer _oscQueryServer;
    private readonly ShockOscData _dataLayer;

    private bool _oscServerActive;
    private bool _isAfk;
    private bool _isMuted;
    public string AvatarId = string.Empty;
    private readonly Random Random = new();

    public event Func<Task>? OnGroupsChanged;

    public static readonly string[] ShockerParams =
    {
        string.Empty,
        "Stretch",
        "IsGrabbed",
        "Cooldown",
        "Active",
        "Intensity",
        "CooldownPercentage",
        "IShock"
    };

    public readonly Dictionary<string, object?> ShockOscParams = new();
    public readonly Dictionary<string, object?> AllAvatarParams = new();

    public Action<bool>? OnParamsChange;

    private readonly ChangeTrackedOscParam<bool> _paramAnyActive;
    private readonly ChangeTrackedOscParam<bool> _paramAnyCooldown;
    private readonly ChangeTrackedOscParam<float> _paramAnyCooldownPercentage;
    private readonly ChangeTrackedOscParam<float> _paramAnyIntensity;

    private string _liveConnectionId = string.Empty;

    public ShockOsc(ILogger<ShockOsc> logger, OscClient oscClient, OpenShockApi openShockApi,
        OpenShockApiLiveClient liveClient, UnderscoreConfig underscoreConfig,
        ConfigManager configManager, OscQueryServer oscQueryServer, ShockOscData dataLayer)
    {
        _logger = logger;
        _oscClient = oscClient;
        _liveClient = liveClient;
        _underscoreConfig = underscoreConfig;
        _configManager = configManager;
        _oscQueryServer = oscQueryServer;
        _dataLayer = dataLayer;

        _paramAnyActive = new ChangeTrackedOscParam<bool>("_Any", "_Active", false, _oscClient);
        _paramAnyCooldown = new ChangeTrackedOscParam<bool>("_Any", "_Cooldown", false, _oscClient);
        _paramAnyCooldownPercentage = new ChangeTrackedOscParam<float>("_Any", "_CooldownPercentage", 0f, _oscClient);
        _paramAnyIntensity = new ChangeTrackedOscParam<float>("_Any", "_Intensity", 0f, _oscClient);

        liveClient.OnWelcome += s =>
        {
            _liveConnectionId = s;
            return Task.CompletedTask;
        };

        liveClient.OnLog += RemoteActivateShockers;

        OnGroupsChanged += SetupGroups;
        
        oscQueryServer.FoundVrcClient += FoundVrcClient;
        oscQueryServer.ParameterUpdate += OnAvatarChange;
        
        SetupGroups().Wait();

        if (!_configManager.Config.Osc.OscQuery)
        {
            FoundVrcClient(null);
        }
        
        _logger.LogInformation("Started ShockOsc.cs");
    }

    private async Task SetupGroups()
    {
        _dataLayer.ProgramGroups.Clear();
        _dataLayer.ProgramGroups[Guid.Empty] = new ProgramGroup(Guid.Empty, "_All", _oscClient);
        foreach (var (id, group) in _configManager.Config.Groups) _dataLayer.ProgramGroups[id] = new ProgramGroup(id, group.Name, _oscClient);
    }

    public Task RaiseOnGroupsChanged() => OnGroupsChanged.Raise();

    private void OnParamChange(bool shockOscParam)
    {
        OnParamsChange?.Invoke(shockOscParam);
    }

    public async Task FoundVrcClient(IPEndPoint? oscClient)
    {
        _logger.LogInformation("Found VRC client");
        // stop tasks
        _oscServerActive = false;
        Task.Delay(1000).Wait(); // wait for tasks to stop

        if (oscClient != null)
        {
            _oscClient.CreateGameConnection(oscClient.Address, _oscQueryServer.ShockOscReceivePort, (ushort)oscClient.Port);
        }
        else
        {
            _oscClient.CreateGameConnection(IPAddress.Loopback, _configManager.Config.Osc.OscReceivePort,
                _configManager.Config.Osc.OscSendPort);
        }

        _logger.LogInformation("Connecting UDP Clients...");

        // Start tasks
        _oscServerActive = true;
        OsTask.Run(ReceiverLoopAsync);
        OsTask.Run(SenderLoopAsync);
        OsTask.Run(CheckLoop);

        _logger.LogInformation("Ready");
        OsTask.Run(_underscoreConfig.SendUpdateForAll);
    }

    public async Task OnAvatarChange(Dictionary<string, object?> parameters, string avatarId)
    {
        AvatarId = avatarId;
        try
        {
            foreach (var obj in _dataLayer.ProgramGroups)
            {
                obj.Value.Reset();
            }

            var parameterCount = 0;

            ShockOscParams.Clear();
            AllAvatarParams.Clear();

            foreach (var param in parameters.Keys)
            {
                if (param.StartsWith("/avatar/parameters/"))
                    AllAvatarParams.TryAdd(param[19..], parameters[param]);

                if (!param.StartsWith("/avatar/parameters/ShockOsc/"))
                    continue;

                var paramName = param[28..];
                var lastUnderscoreIndex = paramName.LastIndexOf('_') + 1;
                var shockerName = paramName;
                // var action = string.Empty;
                if (lastUnderscoreIndex > 1)
                {
                    shockerName = paramName[..(lastUnderscoreIndex - 1)];
                    // action = paramName.Substring(lastUnderscoreIndex, paramName.Length - lastUnderscoreIndex);
                }
                
                parameterCount++;
                ShockOscParams.TryAdd(param[28..], parameters[param]);

                if (!_dataLayer.ProgramGroups.Any(x =>
                        x.Value.Name.Equals(shockerName, StringComparison.InvariantCultureIgnoreCase)) &&
                    !shockerName.StartsWith('_'))
                {
                    _logger.LogWarning("Unknown shocker on avatar {Shocker}", shockerName);
                    _logger.LogDebug("Param: {Param}", param);
                }
            }

            _logger.LogInformation("Loaded avatar config with {ParamCount} parameters", parameterCount);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error on avatar change logic");
        }

        OnParamChange(true);
    }

    private async Task ReceiverLoopAsync()
    {
        while (_oscServerActive)
        {
            try
            {
                await ReceiveLogic();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in receiver loop");
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private async Task ReceiveLogic()
    {
        OscMessage received;
        try
        {
            received = await _oscClient.ReceiveGameMessage()!;
        }
        catch (Exception e)
        {
            _logger.LogTrace(e, "Error receiving message");
            return;
        }

        var addr = received.Address;

        if (addr.StartsWith("/avatar/parameters/"))
        {
            var fullName = addr[19..];
            if (AllAvatarParams.ContainsKey(fullName))
            {
                AllAvatarParams[fullName] = received.Arguments[0];
                OnParamChange(false);
            }
            else
                AllAvatarParams.TryAdd(fullName, received.Arguments[0]);
        }

        switch (addr)
        {
            case "/avatar/change":
                var avatarId = received.Arguments.ElementAtOrDefault(0);
                _logger.LogDebug("Avatar changed: {AvatarId}", avatarId);
                OsTask.Run(_oscQueryServer.GetParameters);
                OsTask.Run(_underscoreConfig.SendUpdateForAll);
                return;
            case "/avatar/parameters/AFK":
                _isAfk = received.Arguments.ElementAtOrDefault(0) is true;
                _logger.LogDebug("Afk: {State}", _isAfk);
                return;
            case "/avatar/parameters/MuteSelf":
                _isMuted = received.Arguments.ElementAtOrDefault(0) is true;
                _logger.LogDebug("Muted: {State}", _isMuted);
                return;
        }

        if (!addr.StartsWith("/avatar/parameters/ShockOsc/"))
            return;

        var pos = addr.Substring(28, addr.Length - 28);

        if (ShockOscParams.ContainsKey(pos))
        {
            ShockOscParams[pos] = received.Arguments[0];
            OnParamChange(true);
        }
        else
            ShockOscParams.TryAdd(pos, received.Arguments[0]);

        // Check if _Config
        if (pos.StartsWith("_Config/"))
        {
            _underscoreConfig.HandleCommand(pos, received.Arguments);
            return;
        }

        var lastUnderscoreIndex = pos.LastIndexOf('_') + 1;
        var action = string.Empty;
        var groupName = pos;
        if (lastUnderscoreIndex > 1)
        {
            groupName = pos[..(lastUnderscoreIndex - 1)];
            action = pos.Substring(lastUnderscoreIndex, pos.Length - lastUnderscoreIndex);
        }

        if (!ShockerParams.Contains(action)) return;

        if (!_dataLayer.ProgramGroups.Any(x => x.Value.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase)))
        {
            if (groupName == "_Any") return;
            _logger.LogWarning("Unknown group {GroupName}", groupName);
            _logger.LogDebug("Param: {Param}", pos);
            return;
        }

        var programGroup = _dataLayer.ProgramGroups
            .First(x => x.Value.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase)).Value;

        var value = received.Arguments.ElementAtOrDefault(0);
        switch (action)
        {
            case "IShock":
                // TODO: check Cooldowns
                if (value is not true) return;
                if (_underscoreConfig.KillSwitch)
                {
                    programGroup.TriggerMethod = TriggerMethod.None;
                    await LogIgnoredKillSwitchActive();
                    return;
                }

                if (_isAfk && _configManager.Config.Behaviour.DisableWhileAfk)
                {
                    programGroup.TriggerMethod = TriggerMethod.None;
                    await LogIgnoredAfk();
                    return;
                }

                OsTask.Run(() => InstantShock(programGroup, GetDuration(), GetIntensity()));

                return;
            case "Stretch":
                if (value is float stretch)
                    programGroup.LastStretchValue = stretch;
                return;
            case "IsGrabbed":
                var isGrabbed = value is true;
                if (programGroup.IsGrabbed && !isGrabbed)
                {
                    // on physbone release
                    if (programGroup.LastStretchValue != 0)
                    {
                        programGroup.TriggerMethod = TriggerMethod.PhysBoneRelease;
                        programGroup.LastActive = DateTime.UtcNow;
                    }
                    else if (_configManager.Config.Behaviour.WhileBoneHeld !=
                             BehaviourConf.BoneHeldAction.None)
                    {
                        await CancelAction(programGroup);
                    }
                }

                programGroup.IsGrabbed = isGrabbed;
                return;
            // Normal shocker actions
            case "":
                break;
            // Ignore all other actions
            default:
                return;
        }

        if (value is true)
        {
            programGroup.TriggerMethod = TriggerMethod.Manual;
            programGroup.LastActive = DateTime.UtcNow;
        }
        else programGroup.TriggerMethod = TriggerMethod.None;
    }

    private ValueTask LogIgnoredKillSwitchActive()
    {
        _logger.LogInformation("Ignoring shock, kill switch is active");
        if (string.IsNullOrEmpty(_configManager.Config.Chatbox.IgnoredKillSwitchActive))
            return ValueTask.CompletedTask;

        return _oscClient.SendChatboxMessage(
            $"{_configManager.Config.Chatbox.Prefix}{_configManager.Config.Chatbox.IgnoredKillSwitchActive}");
    }

    private ValueTask LogIgnoredAfk()
    {
        _logger.LogInformation("Ignoring shock, user is AFK");
        if (string.IsNullOrEmpty(_configManager.Config.Chatbox.IgnoredAfk))
            return ValueTask.CompletedTask;

        return _oscClient.SendChatboxMessage(
            $"{_configManager.Config.Chatbox.Prefix}{_configManager.Config.Chatbox.IgnoredAfk}");
    }

    private async Task SenderLoopAsync()
    {
        while (_oscServerActive)
        {
            await SendParams();
            await Task.Delay(300);
        }
    }

    private async Task InstantShock(ProgramGroup programGroup, uint duration, byte intensity)
    {
        programGroup.LastExecuted = DateTime.UtcNow;
        programGroup.LastDuration = duration;
        var intensityPercentage = Math.Round(MathUtils.ClampFloat(intensity) * 100f);
        programGroup.LastIntensity = intensity;

        ForceUnmute();
        SendParams();

        programGroup.TriggerMethod = TriggerMethod.None;
        var inSeconds = MathF.Round(duration / 1000f, 1).ToString(CultureInfo.InvariantCulture);
        _logger.LogInformation(
            "Sending shock to {GroupName} Intensity: {Intensity} IntensityPercentage: {IntensityPercentage}% Length:{Length}s",
            programGroup.Name, intensity, intensityPercentage, inSeconds);

        await ControlGroup(programGroup.Id, duration, intensity, ControlType.Shock);

        if (!_configManager.Config.Osc.Chatbox) return;
        // Chatbox message local
        var dat = new
        {
            ShockerName = programGroup.Name,
            Intensity = intensity,
            IntensityPercentage = intensityPercentage,
            Duration = duration,
            DurationSeconds = inSeconds
        };
        var template = _configManager.Config.Chatbox.Types[ControlType.Shock];
        var msg = $"{_configManager.Config.Chatbox.Prefix}{Smart.Format(template.Local, dat)}";
        await _oscClient.SendChatboxMessage(msg);
    }

    // /// <summary>
    // /// Coverts to a 0-1 float and scale it to the max intensity
    // /// </summary>
    // /// <param name="intensity"></param>
    // /// <returns></returns>
    // private static float GetFloatScaled(byte intensity) =>
    //     ClampFloat((float)intensity / _configManager.ConfigInstance.Behaviour.IntensityRange.Max);

    private async Task SendParams()
    {
        // TODO: maybe force resend on avatar change
        var anyActive = false;
        var anyCooldown = false;
        var anyCooldownPercentage = 0f;
        var anyIntensity = 0f;

        foreach (var shocker in _dataLayer.ProgramGroups.Values)
        {
            var isActive = shocker.LastExecuted.AddMilliseconds(shocker.LastDuration) > DateTime.UtcNow;
            var isActiveOrOnCooldown =
                shocker.LastExecuted.AddMilliseconds(_configManager.Config.Behaviour.CooldownTime)
                    .AddMilliseconds(shocker.LastDuration) > DateTime.UtcNow;
            if (!isActiveOrOnCooldown && shocker.LastIntensity > 0)
                shocker.LastIntensity = 0;

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
            await shocker.ParamIntensity.SetValue(MathUtils.ClampFloat(shocker.LastIntensity));

            if (isActive) anyActive = true;
            if (onCoolDown) anyCooldown = true;
            anyCooldownPercentage = Math.Max(anyCooldownPercentage, cooldownPercentage);
            anyIntensity = Math.Max(anyIntensity, MathUtils.ClampFloat(shocker.LastIntensity));
        }

        await _paramAnyActive.SetValue(anyActive);
        await _paramAnyCooldown.SetValue(anyCooldown);
        await _paramAnyCooldownPercentage.SetValue(anyCooldownPercentage);
        await _paramAnyIntensity.SetValue(anyIntensity);
    }

    private async Task CheckLoop()
    {
        while (_oscServerActive)
        {
            try
            {
                await CheckLogic();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in check loop");
            }

            await Task.Delay(20);
        }
    }

    private byte GetIntensity()
    {
        var config = _configManager.Config.Behaviour;

        if (!config.RandomIntensity) return config.FixedIntensity;
        var rir = config.IntensityRange;
        var intensityValue = Random.Next((int)rir.Min, (int)rir.Max);
        return (byte)intensityValue;
    }

    private async Task CheckLogic()
    {
        var config = _configManager.Config.Behaviour;
        foreach (var (pos, programGroup) in _dataLayer.ProgramGroups)
        {
            var isActiveOrOnCooldown =
                programGroup.LastExecuted.AddMilliseconds(_configManager.Config.Behaviour.CooldownTime)
                    .AddMilliseconds(programGroup.LastDuration) > DateTime.UtcNow;

            if (programGroup.TriggerMethod == TriggerMethod.None &&
                _configManager.Config.Behaviour.WhileBoneHeld !=
                BehaviourConf.BoneHeldAction.None &&
                !isActiveOrOnCooldown &&
                programGroup.IsGrabbed &&
                programGroup.LastVibration < DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(300)))
            {
                var vibrationIntensity = programGroup.LastStretchValue * 100f;
                if (vibrationIntensity < 1)
                    vibrationIntensity = 1;
                programGroup.LastVibration = DateTime.UtcNow;

                _logger.LogDebug("Vibrating {Shocker} at {Intensity}", pos, vibrationIntensity);
                await ControlGroup(programGroup.Id, 1000, (byte)vibrationIntensity,
                    _configManager.Config.Behaviour.WhileBoneHeld ==
                    BehaviourConf.BoneHeldAction.Shock
                        ? ControlType.Shock
                        : ControlType.Vibrate);
            }

            if (programGroup.TriggerMethod == TriggerMethod.None)
                continue;

            if (programGroup.TriggerMethod == TriggerMethod.Manual &&
                programGroup.LastActive.AddMilliseconds(config.HoldTime) > DateTime.UtcNow)
                continue;

            if (isActiveOrOnCooldown)
            {
                programGroup.TriggerMethod = TriggerMethod.None;
                _logger.LogInformation("Ignoring shock, group {Shocker} is on cooldown", pos);
                continue;
            }

            if (_underscoreConfig.KillSwitch)
            {
                programGroup.TriggerMethod = TriggerMethod.None;
                await LogIgnoredKillSwitchActive();
                continue;
            }

            if (_isAfk && config.DisableWhileAfk)
            {
                programGroup.TriggerMethod = TriggerMethod.None;
                await LogIgnoredAfk();
                continue;
            }

            byte intensity;

            if (programGroup.TriggerMethod == TriggerMethod.PhysBoneRelease)
            {
                intensity = (byte)MathUtils.LerpFloat(config.IntensityRange.Min, config.IntensityRange.Max,
                    programGroup.LastStretchValue);
                programGroup.LastStretchValue = 0;
            }
            else intensity = GetIntensity();

            InstantShock(programGroup, GetDuration(), intensity);
        }
    }

    private uint GetDuration()
    {
        var config = _configManager.Config.Behaviour;

        if (!config.RandomDuration) return config.FixedDuration;
        var rdr = config.DurationRange;
        return (uint)(Random.Next((int)(rdr.Min / config.RandomDurationStep),
            (int)(rdr.Max / config.RandomDurationStep)) * config.RandomDurationStep);
    }

    private async Task<bool> ControlGroup(Guid groupId, uint duration, byte intensity, ControlType type)
    {
        if(groupId == Guid.Empty)
        {
            var controlCommandsAll = _configManager.Config.OpenShock.Shockers
                .Where(x => x.Value.Enabled)
                .Select(x => new Control
                {
                    Id = x.Key,
                    Duration = duration,
                    Intensity = intensity,
                    Type = type
                });
            await _liveClient.Control(controlCommandsAll);
            return true;
        }
        
        
        if (!_configManager.Config.Groups.TryGetValue(groupId, out var group)) return false;

        var controlCommands = group.Shockers.Select(x => new Control
        {
            Id = x,
            Duration = duration,
            Intensity = intensity,
            Type = type
        });

        await _liveClient.Control(controlCommands);
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
        if (_configManager.Config.Osc.Chatbox &&
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
        
        var configGroupsAffected = _configManager.Config.Groups.Where(s => s.Value.Shockers.Any(x => x == log.Shocker.Id)).Select(x => x.Key).ToArray();
        var programGroupsAffected = _dataLayer.ProgramGroups.Where(x => configGroupsAffected.Contains(x.Key)).Select(x => x.Value);
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
                    SendParams();
                    break;
                case ControlType.Sound:
                    break;
                default:
                    _logger.LogError("ControlType was out of range. Value was: {Type}", log.Type);
                    break;
            }
        
            if (oneShock)
            {
                ForceUnmute();
                SendParams();
            }
        }
    }

    private async Task ForceUnmute()
    {
        if (!_configManager.Config.Behaviour.ForceUnmute || !_isMuted) return;
        _logger.LogDebug("Force unmuting...");
        await _oscClient.SendGameMessage("/input/Voice", false);
        await Task.Delay(50);
        await _oscClient.SendGameMessage("/input/Voice", true);
        await Task.Delay(50);
        await _oscClient.SendGameMessage("/input/Voice", false);
    }

    private Task CancelAction(ProgramGroup programGroup)
    {
        _logger.LogDebug("Cancelling action");
        return ControlGroup(programGroup.Id, 0, 0, ControlType.Stop);
    }


}