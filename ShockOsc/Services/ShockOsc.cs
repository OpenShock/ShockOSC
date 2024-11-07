using System.Globalization;
using System.Net;
using LucHeart.CoreOSC;
using OpenShock.SDK.CSharp.Models;
using OpenShock.SDK.CSharp.Utils;
using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Models;
using OpenShock.ShockOsc.Utils;
using OscQueryLibrary;

#pragma warning disable CS4014

namespace OpenShock.ShockOsc.Services;

public sealed class ShockOsc
{
    private readonly ILogger<ShockOsc> _logger;
    private readonly OscClient _oscClient;
    private readonly BackendHubManager _backendHubManager;
    private readonly UnderscoreConfig _underscoreConfig;
    private readonly ConfigManager _configManager;
    private readonly OscQueryServer _oscQueryServer;
    private readonly ShockOscData _dataLayer;
    private readonly OscHandler _oscHandler;
    private readonly LiveControlManager _liveControlManager;
    private readonly ChatboxService _chatboxService;

    private bool _oscServerActive;
    private bool _isAfk;
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
        "IShock",
        "IVibrate",
        "ISound",
        "CShock",
        "NextIntensity",
        "NextDuration",
    };

    public readonly Dictionary<string, object?> ShockOscParams = new();
    public readonly Dictionary<string, object?> AllAvatarParams = new();

    public Action<bool>? OnParamsChange;


    public ShockOsc(ILogger<ShockOsc> logger,
        OscClient oscClient,
        BackendHubManager backendHubManager,
        UnderscoreConfig underscoreConfig,
        ConfigManager configManager,
        OscQueryServer oscQueryServer,
        ShockOscData dataLayer,
        OscHandler oscHandler, LiveControlManager liveControlManager,
        ChatboxService chatboxService)
    {
        _logger = logger;
        _oscClient = oscClient;
        _backendHubManager = backendHubManager;
        _underscoreConfig = underscoreConfig;
        _configManager = configManager;
        _oscQueryServer = oscQueryServer;
        _dataLayer = dataLayer;
        _oscHandler = oscHandler;
        _liveControlManager = liveControlManager;
        _chatboxService = chatboxService;

        OnGroupsChanged += () =>
        {
            SetupGroups();
            return Task.CompletedTask;
        };

        oscQueryServer.FoundVrcClient += FoundVrcClient;
        oscQueryServer.ParameterUpdate += OnAvatarChange;

        SetupGroups();

        if (!_configManager.Config.Osc.OscQuery)
        {
            FoundVrcClient(null, null);
        }

        _logger.LogInformation("Started ShockOsc.cs");
    }

    private void SetupGroups()
    {
        _dataLayer.ProgramGroups.Clear();
        _dataLayer.ProgramGroups[Guid.Empty] = new ProgramGroup(Guid.Empty, "_All", _oscClient, null);
        foreach (var (id, group) in _configManager.Config.Groups)
            _dataLayer.ProgramGroups[id] = new ProgramGroup(id, group.Name, _oscClient, group);
    }

    public Task RaiseOnGroupsChanged() => OnGroupsChanged.Raise();

    private void OnParamChange(bool shockOscParam)
    {
        OnParamsChange?.Invoke(shockOscParam);
    }

    private async Task FoundVrcClient(OscQueryServer oscQueryServer, IPEndPoint ipEndPoint)
    {
        _logger.LogInformation("Found VRC client at {Ip}", ipEndPoint);
        // stop tasks
        _oscServerActive = false;
        Task.Delay(1000).Wait(); // wait for tasks to stop

        if (ipEndPoint != null)
        {
            _oscClient.CreateGameConnection(ipEndPoint.Address, oscQueryServer.OscReceivePort,
                (ushort)ipEndPoint.Port);
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

        await _chatboxService.SendGenericMessage("Game Connected");
    }

    private Task OnAvatarChange(Dictionary<string, object?> parameters, string avatarId)
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
        return Task.CompletedTask;
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
            // FIXME: less alloc pls
            var fullName = addr[19..];
            if (AllAvatarParams.ContainsKey(fullName))
                AllAvatarParams[fullName] = received.Arguments[0];
            else
                AllAvatarParams.TryAdd(fullName, received.Arguments[0]);
            OnParamChange(false);
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
                _dataLayer.IsMuted = received.Arguments.ElementAtOrDefault(0) is true;
                _logger.LogDebug("Muted: {State}", _dataLayer.IsMuted);
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

        if (!_dataLayer.ProgramGroups.Any(x =>
                x.Value.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase)))
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
            case "NextIntensity":
                if (value is not float nextIntensity)
                {
                    programGroup.NextIntensity = 0;
                    return;
                }

                programGroup.NextIntensity = Convert.ToByte(MathUtils.Saturate(nextIntensity) * 100f);
                break;
            
            case "NextDuration":
                if (value is not float nextDuration)
                {
                    programGroup.NextDuration = 0;
                    return;
                }

                programGroup.NextDuration = nextDuration;
                break;
            
            case "CShock":
                if (value is not float intensity)
                {
                    programGroup.ConcurrentIntensity = 0;
                    programGroup.ConcurrentType = ControlType.Stop;
                    return;
                }
                
                var scaledIntensity = intensity * 100f;
                if(scaledIntensity > 127) break;
                
                programGroup.ConcurrentIntensity = Convert.ToByte(intensity * 100f);

                var ctype = action switch
                {
                    "CShock" => ControlType.Shock,
                    "CVibrate" => ControlType.Vibrate,
                    "CSound" => ControlType.Sound,
                    _ => ControlType.Vibrate
                };

                programGroup.ConcurrentType = ctype;
                break;
            
            case "IShock":
            case "IVibrate":
            case "ISound":
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

                var cooldownTime = _configManager.Config.Behaviour.CooldownTime;
                if (programGroup.ConfigGroup is { OverrideCooldownTime: true })
                    cooldownTime = programGroup.ConfigGroup.CooldownTime;

                var isActiveOrOnCooldown =
                    programGroup.LastExecuted.AddMilliseconds(cooldownTime)
                        .AddMilliseconds(programGroup.LastDuration) > DateTime.UtcNow;

                if (isActiveOrOnCooldown)
                {
                    programGroup.TriggerMethod = TriggerMethod.None;
                    _logger.LogInformation("Ignoring IShock, group {Group} is on cooldown", programGroup.Name);
                    return;
                }

                var type = action switch
                {
                    "IShock" => ControlType.Shock,
                    "IVibrate" => ControlType.Vibrate,
                    "ISound" => ControlType.Sound,
                    _ => ControlType.Vibrate
                };

                OsTask.Run(() =>
                    InstantAction(programGroup, GetDuration(programGroup), GetIntensity(programGroup), type));

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
                        await _backendHubManager.CancelControl(programGroup);
                    }
                }

                if (!programGroup.IsGrabbed && isGrabbed)
                {
                    // on physbone grab
                    ushort TheDuration = GetDuration(programGroup);
                    programGroup.PhysBoneGrabLimitTime = DateTime.UtcNow.AddMilliseconds(TheDuration);
                    _logger.LogDebug("Limiting hold duration of Group {Group} to {Duration}ms", programGroup.Name, TheDuration);
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

        return _chatboxService.SendGenericMessage(_configManager.Config.Chatbox.IgnoredKillSwitchActive);
    }

    private ValueTask LogIgnoredAfk()
    {
        _logger.LogInformation("Ignoring shock, user is AFK");
        if (string.IsNullOrEmpty(_configManager.Config.Chatbox.IgnoredAfk))
            return ValueTask.CompletedTask;

        return _chatboxService.SendGenericMessage(_configManager.Config.Chatbox.IgnoredAfk);
    }

    private async Task SenderLoopAsync()
    {
        while (_oscServerActive)
        {
            await _oscHandler.SendParams();
            await Task.Delay(300);
        }
    }

    private async Task InstantAction(ProgramGroup programGroup, ushort duration, byte intensity, ControlType type,
        bool exclusive = false)
    {
        // Intensity is pre scaled to 0 - 100
        var actualIntensity = programGroup.NextIntensity == 0 ? intensity : programGroup.NextIntensity;
        var actualDuration = programGroup.NextDuration == 0 ? duration : GetScaledDuration(programGroup, programGroup.NextDuration);
        
        if (type == ControlType.Shock)
        {
            programGroup.LastExecuted = DateTime.UtcNow;
            programGroup.LastDuration = actualDuration;
            programGroup.LastIntensity = actualIntensity;
            _oscHandler.ForceUnmute();
            _oscHandler.SendParams();
        }

        programGroup.TriggerMethod = TriggerMethod.None;
        var inSeconds = MathF.Round(actualDuration / 1000f, 1).ToString(CultureInfo.InvariantCulture);
        _logger.LogInformation(
            "Sending {Type} to {GroupName} Intensity: {Intensity} Length:{Length}s Exclusive: {Exclusive}", type,
            programGroup.Name, actualIntensity, inSeconds, exclusive);

        await _backendHubManager.ControlGroup(programGroup.Id, actualDuration, actualIntensity, type, exclusive);
        await _chatboxService.SendLocalControlMessage(programGroup.Name, actualIntensity, actualDuration, type);
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

    private async Task CheckLogic()
    {
        var config = _configManager.Config.Behaviour;
        foreach (var (pos, programGroup) in _dataLayer.ProgramGroups)
        {
            await CheckProgramGroup(programGroup, pos, config);
        }
    }

    private async Task CheckProgramGroup(ProgramGroup programGroup, Guid pos, BehaviourConf config)
    {
        if (programGroup.ConcurrentIntensity != 0)
        {
            _liveControlManager.ControlGroupFrameCheckLoop(programGroup, GetScaledIntensity(programGroup, programGroup.ConcurrentIntensity), programGroup.ConcurrentType);
            programGroup.LastConcurrentIntensity = programGroup.ConcurrentIntensity;
            return;
        }

        if (programGroup.LastConcurrentIntensity != 0)
        {
            _liveControlManager.ControlGroupFrameCheckLoop(programGroup, 0, ControlType.Stop);
            programGroup.LastConcurrentIntensity = 0;
            _logger.LogInformation("Stopping");
        }

        var cooldownTime = _configManager.Config.Behaviour.CooldownTime;
        if (programGroup.ConfigGroup is { OverrideCooldownTime: true })
            cooldownTime = programGroup.ConfigGroup.CooldownTime;

        var isActiveOrOnCooldown =
            programGroup.LastExecuted.AddMilliseconds(cooldownTime)
                .AddMilliseconds(programGroup.LastDuration) > DateTime.UtcNow;

        if (programGroup.TriggerMethod == TriggerMethod.None &&
            _configManager.Config.Behaviour.WhileBoneHeld !=
            BehaviourConf.BoneHeldAction.None &&
            !isActiveOrOnCooldown &&
            !_underscoreConfig.KillSwitch &&
            programGroup.IsGrabbed &&
            programGroup.PhysBoneGrabLimitTime > DateTime.UtcNow &&
            programGroup.LastVibration < DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(100)))
        {
            var pullIntensityTranslated = GetPhysbonePullIntensity(programGroup, programGroup.LastStretchValue);
            programGroup.LastVibration = DateTime.UtcNow;

            _logger.LogDebug("Vibrating/Shocking {Shocker} at {Intensity}", pos, pullIntensityTranslated);

            _liveControlManager.ControlGroupFrameCheckLoop(programGroup, pullIntensityTranslated, _configManager.Config.Behaviour.WhileBoneHeld == BehaviourConf.BoneHeldAction.Shock
                ? ControlType.Shock
                : ControlType.Vibrate);
        }

        if (programGroup.TriggerMethod == TriggerMethod.None)
            return;

        if (programGroup.TriggerMethod == TriggerMethod.Manual &&
            programGroup.LastActive.AddMilliseconds(config.HoldTime) > DateTime.UtcNow)
            return;

        if (isActiveOrOnCooldown)
        {
            programGroup.TriggerMethod = TriggerMethod.None;
            _logger.LogInformation("Ignoring shock, group {Shocker} is on cooldown", pos);
            return;
        }

        if (_underscoreConfig.KillSwitch)
        {
            programGroup.TriggerMethod = TriggerMethod.None;
            await LogIgnoredKillSwitchActive();
            return;
        }

        if (_isAfk && config.DisableWhileAfk)
        {
            programGroup.TriggerMethod = TriggerMethod.None;
            await LogIgnoredAfk();
            return;
        }

        byte intensity;
        var exclusive = false;

        if (programGroup.TriggerMethod == TriggerMethod.PhysBoneRelease)
        {
            if (programGroup.ConfigGroup is { SuppressPhysBoneReleaseAction: true }) { return; }
            intensity = GetPhysbonePullIntensity(programGroup, programGroup.LastStretchValue);
            programGroup.LastStretchValue = 0;

            exclusive = true;
        }
        else intensity = GetIntensity(programGroup);

        InstantAction(programGroup, GetDuration(programGroup), intensity, ControlType.Shock, exclusive);
    }
    
    private ushort GetScaledDuration(ProgramGroup programGroup, float scale)
    {
        scale = MathUtils.Saturate(scale);
        
        if (programGroup.ConfigGroup is not { OverrideDuration: true })
        {
            // Use global config
            var config = _configManager.Config.Behaviour;

            if (!config.RandomDuration) return (ushort) (config.FixedDuration * scale);
            var rdr = config.DurationRange;
            return (ushort)
                (MathUtils.LerpUShort(
                    (ushort)(rdr.Min / DurationStep), (ushort)(rdr.Max / DurationStep), scale)
                 * DurationStep);
        }

        // Use group config
        var groupConfig = programGroup.ConfigGroup;

        if (!groupConfig.RandomDuration) return (ushort) (groupConfig.FixedDuration * scale);
        var groupRdr = groupConfig.DurationRange;
        return (ushort)(MathUtils.LerpUShort((ushort) (groupRdr.Min / DurationStep),
            (ushort)(groupRdr.Max / DurationStep), scale) * DurationStep);
    }
    
    private byte GetScaledIntensity(ProgramGroup programGroup, byte intensity)
    {
        if (programGroup.ConfigGroup is not { OverrideIntensity: true })
        {
            // Use global config
            var config = _configManager.Config.Behaviour;

            if (!config.RandomIntensity) return (byte)MathUtils.LerpFloat(0, config.FixedIntensity, intensity / 100f);
            return (byte)MathUtils.LerpFloat(config.IntensityRange.Min, config.IntensityRange.Max, intensity / 100f);
        }

        // Use group config
        var groupConfig = programGroup.ConfigGroup;

        if (!groupConfig.RandomIntensity) return (byte)MathUtils.LerpFloat(0, groupConfig.FixedIntensity, intensity / 100f);
        return (byte)MathUtils.LerpFloat(groupConfig.IntensityRange.Min, groupConfig.IntensityRange.Max, intensity / 100f);
    }

    private byte GetPhysbonePullIntensity(ProgramGroup programGroup, float stretch)
    {
        stretch = MathUtils.Saturate(stretch);
        if (programGroup.ConfigGroup is not { OverrideIntensity: true })
        {
            // Use global config
            var config = _configManager.Config.Behaviour;

            if (!config.RandomIntensity) return config.FixedIntensity;
            return (byte)MathUtils.LerpFloat(config.IntensityRange.Min, config.IntensityRange.Max, stretch);
        }

        // Use group config
        var groupConfig = programGroup.ConfigGroup;

        if (!groupConfig.RandomIntensity) return groupConfig.FixedIntensity;
        return (byte)MathUtils.LerpFloat(groupConfig.IntensityRange.Min, groupConfig.IntensityRange.Max, stretch);
    }

    private const ushort DurationStep = 100;

    private ushort GetDuration(ProgramGroup programGroup)
    {
        if (programGroup.ConfigGroup is not { OverrideDuration: true })
        {
            // Use global config
            var config = _configManager.Config.Behaviour;

            if (!config.RandomDuration) return config.FixedDuration;
            var rdr = config.DurationRange;
            return (ushort)(Random.Next(rdr.Min / DurationStep,
                rdr.Max / DurationStep) * DurationStep);
        }

        // Use group config
        var groupConfig = programGroup.ConfigGroup;

        if (!groupConfig.RandomDuration) return groupConfig.FixedDuration;
        var groupRdr = groupConfig.DurationRange;
        return (ushort)(Random.Next(groupRdr.Min / DurationStep,
            groupRdr.Max / DurationStep) * DurationStep);
    }

    private byte GetIntensity(ProgramGroup programGroup)
    {
        if (programGroup.ConfigGroup is not { OverrideDuration: true })
        {
            // Use global config
            var config = _configManager.Config.Behaviour;

            if (!config.RandomIntensity) return config.FixedIntensity;
            var rir = config.IntensityRange;
            var intensityValue = Random.Next(rir.Min, rir.Max);
            return (byte)intensityValue;
        }

        // Use groupConfig
        var groupConfig = programGroup.ConfigGroup;

        if (!groupConfig.RandomIntensity) return groupConfig.FixedIntensity;
        var groupRir = groupConfig.IntensityRange;
        var groupIntensityValue = Random.Next(groupRir.Min, groupRir.Max);
        return (byte)groupIntensityValue;
    }
}