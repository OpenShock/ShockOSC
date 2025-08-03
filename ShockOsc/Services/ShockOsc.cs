using System.Globalization;
using System.Net;
using System.Reactive.Subjects;
using LucHeart.CoreOSC;
using Microsoft.Extensions.Logging;
using MudBlazor.Extensions;
using OneOf.Types;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.MinimalEvents;
using OpenShock.ShockOSC.Config;
using OpenShock.ShockOSC.Models;
using OpenShock.ShockOSC.Utils;
using OscQueryLibrary;
using OscQueryLibrary.Utils;
using Serilog;

#pragma warning disable CS4014

namespace OpenShock.ShockOSC.Services;

public sealed class ShockOsc
{
    private readonly ILogger<ShockOsc> _logger;
    private readonly OscClient _oscClient;
    private readonly IOpenShockService _openShockService;
    private readonly UnderscoreConfig _underscoreConfig;
    private readonly IModuleConfig<ShockOscConfig> _moduleConfig;
    private readonly OscQueryServer _oscQueryServer;
    private readonly ShockOscData _dataLayer;
    private readonly OscHandler _oscHandler;
    private readonly ChatboxService _chatboxService;

    private bool _oscServerActive;
    private bool _isAfk;
    public string AvatarId = string.Empty;
    private readonly Random Random = new();

    private readonly MinimalEvent _onGroupsChanged = new();

    private static readonly string[] ShockerParams =
    [
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
        "CVibrate",
        "CSound",
        "NextIntensity",
        "NextDuration"
    ];

    public readonly Dictionary<string, object?> ShockOscParams = new();
    public readonly Dictionary<string, object?> AllAvatarParams = new();

    public IObservable<bool> OnParamsChangeObservable => _onParamsChange;
    private readonly Subject<bool> _onParamsChange = new();

    public ShockOsc(ILogger<ShockOsc> logger,
        OscClient oscClient,
        IOpenShockService openShockService,
        UnderscoreConfig underscoreConfig,
        IModuleConfig<ShockOscConfig> moduleConfig,
        OscQueryServer oscQueryServer,
        ShockOscData dataLayer,
        OscHandler oscHandler,
        ChatboxService chatboxService)
    {
        _logger = logger;
        _oscClient = oscClient;
        _openShockService = openShockService;
        _underscoreConfig = underscoreConfig;
        _moduleConfig = moduleConfig;
        _oscQueryServer = oscQueryServer;
        _dataLayer = dataLayer;
        _oscHandler = oscHandler;
        _chatboxService = chatboxService;

        _onGroupsChanged.Subscribe(SetupGroups);

        oscQueryServer.FoundVrcClient.SubscribeAsync(endPoint => SetupVrcClient((oscQueryServer, endPoint))).AsTask()
            .Wait();
        oscQueryServer.ParameterUpdate.SubscribeAsync(OnAvatarChange).AsTask().Wait();

        SetupGroups();
    }

    public async Task Start()
    {
        if (!_moduleConfig.Config.Osc.OscQuery)
        {
            await SetupVrcClient(null);
        }
    }

    private void SetupGroups()
    {
        _dataLayer.ProgramGroups.Clear();
        _dataLayer.ProgramGroups[Guid.Empty] = new ProgramGroup(Guid.Empty, "_All", _oscClient, null);
        foreach (var (id, group) in _moduleConfig.Config.Groups)
            _dataLayer.ProgramGroups[id] = new ProgramGroup(id, group.Name, _oscClient, group);
    }

    public void RaiseOnGroupsChanged() => _onGroupsChanged.Invoke();

    private async Task SetupVrcClient((OscQueryServer, IPEndPoint)? client)
    {
        // stop tasks
        _oscServerActive = false;
        await Task.Delay(1000); // wait for tasks to stop TODO: REWORK THIS

        if (client != null)
        {
            _logger.LogInformation("Found VRC client at {Ip}", client.Value.Item2);
            _oscClient.CreateGameConnection(client.Value.Item2.Address, client.Value.Item1.OscReceivePort,
                (ushort)client.Value.Item2.Port);
        }
        else
        {
            _oscClient.CreateGameConnection(IPAddress.Parse(_moduleConfig.Config.Osc.OscSendIp), _moduleConfig.Config.Osc.OscReceivePort,
                _moduleConfig.Config.Osc.OscSendPort);
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

    private Task OnAvatarChange(OscQueryServer.ParameterUpdateArgs parameterUpdateArgs)
    {
        AvatarId = parameterUpdateArgs.AvatarId;
        var parameters = parameterUpdateArgs.Parameters;
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

        _onParamsChange.OnNext(true);
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
            _onParamsChange.OnNext(false);
        }

        switch (addr)
        {
            case "/avatar/change":
                var avatarId = received.Arguments.ElementAtOrDefault(0);
                _logger.LogDebug("Avatar changed: {AvatarId}", avatarId);
                OsTask.Run(_oscQueryServer.RefreshParameters);
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
            _onParamsChange.OnNext(true);
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
            case "CVibrate":
            case "CSound":
                if (value is not float intensity)
                {
                    programGroup.ConcurrentIntensity = 0;
                    programGroup.ConcurrentType = ControlType.Stop;
                    return;
                }

                var scaledIntensity = MathUtils.Saturate(intensity) * 100f;
                programGroup.ConcurrentIntensity = Convert.ToByte(scaledIntensity);

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

                if (!await HandlePrecondition(CheckAndSetAllPreconditions(programGroup), programGroup)) return;
                

                var type = action switch
                {
                    "IShock" => ControlType.Shock,
                    "IVibrate" => ControlType.Vibrate,
                    "ISound" => ControlType.Sound,
                    _ => ControlType.Vibrate
                };

                OsTask.Run(() =>
                    SendCommand(programGroup, GetDuration(programGroup), GetIntensity(programGroup), type));

                return;
            case "Stretch":
                if (value is float stretch)
                    programGroup.LastStretchValue = stretch;
                return;
            case "IsGrabbed":
                var isGrabbed = value is true;
                await PhysboneHandling(programGroup, isGrabbed);

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

    private async Task PhysboneHandling(ProgramGroup programGroup, bool isGrabbed)
    {
        switch (programGroup.IsGrabbed)
        {
            // Physbone was grabbed, and is now released
            case true when !isGrabbed:
            {
                programGroup.TriggerMethod = TriggerMethod.None;
                
                // When the stretch value is not 0, we send the action
                if (programGroup.LastStretchValue != 0)
                {
                    
                    // Check all preconditions, maybe send stop command here aswell?
                    if (!await HandlePrecondition(CheckAndSetAllPreconditions(programGroup), programGroup)) return;
                        
                    var pullTriggerBehavior = _moduleConfig.Config.GetGroupOrGlobal(programGroup,
                        behaviourConfig => behaviourConfig.OnPullTriggerRandomBehavior,
                        group => group.OnPullTriggerRandomBehavior);

                    if (pullTriggerBehavior)
                    {
                        SendCommand(programGroup, GetDuration(programGroup), GetIntensity(programGroup), ControlType.Shock, false);
                    
                        return;
                    }
                    
                    var releaseAction = _moduleConfig.Config.GetGroupOrGlobal(programGroup,
                        behaviourConfig => behaviourConfig.WhenBoneReleased,
                        group => group.OverrideBoneReleasedAction);

                    if (releaseAction == BoneAction.None)
                    {
                        programGroup.LastStretchValue = 0;
                        return;
                    }

                    _logger.LogDebug("Physbone released, sending {Action} to group {Group}", releaseAction, programGroup.Name);
                    _logger.LogInformation("Physbone stretch value: {StretchValue}", programGroup.LastStretchValue);
                    
                    var physBoneIntensity = GetPhysbonePullIntensity(programGroup, programGroup.LastStretchValue);
                    programGroup.LastStretchValue = 0;

                    SendCommand(programGroup, GetDuration(programGroup), physBoneIntensity, releaseAction.ToControlType(),
                        true);
                    
                    return;
                }
                
                // If the stretch value is 0, we stop the group
                if (_moduleConfig.Config.GetGroupOrGlobal(programGroup, config => config.WhileBoneHeld,
                        group => group.OverrideBoneHeldAction) != BoneAction.None)
                {
                    _logger.LogTrace("Physbone released, stopping group {Group}", programGroup.Name);
                    await ControlGroup(programGroup.Id, 0, 0, ControlType.Stop);
                }

                break;
            }
            // Physbone is being grabbed now but was not grabbed before
            case false when isGrabbed:
            {
                // on physbone grab
                var durationLimit = _moduleConfig.Config.GetGroupOrGlobal(programGroup,
                    config => config.BoneHeldDurationLimit, group => group.OverrideBoneHeldDurationLimit);
                programGroup.PhysBoneGrabLimitTime = durationLimit == null
                    ? null
                    : DateTime.UtcNow.AddMilliseconds(durationLimit.Value);
                _logger.LogDebug("Limiting hold duration of Group {Group} to {Duration}ms", programGroup.Name,
                    durationLimit);
                break;
            }
        }
    }
    
    private async ValueTask<bool> HandlePrecondition(OneOf.OneOf<Success, KillSwitch, Cooldown, Paused, Afk> result, ProgramGroup programGroup)
    {
        await result.Match(
            success => ValueTask.CompletedTask,
            killSwitch => LogIgnoredKillSwitchActive(),
            cooldown => ValueTask.CompletedTask,
            paused => LogIgnoredGroupKillSwitchActive(programGroup),
            afk => LogIgnoredAfk());

        return result.IsT0;
    }

    private ValueTask LogIgnoredKillSwitchActive()
    {
        _logger.LogInformation("Ignoring shock, kill switch is active");
        if (string.IsNullOrEmpty(_moduleConfig.Config.Chatbox.IgnoredKillSwitchActive))
            return ValueTask.CompletedTask;

        return _chatboxService.SendGenericMessage(_moduleConfig.Config.Chatbox.IgnoredKillSwitchActive);
    }

    private ValueTask LogIgnoredGroupKillSwitchActive(ProgramGroup programGroup)
    {
        _logger.LogInformation($"Ignoring shock, kill switch of {programGroup.Name} is active");
        if (string.IsNullOrEmpty(_moduleConfig.Config.Chatbox.IgnoredGroupPauseActive))
            return ValueTask.CompletedTask;

        return _chatboxService.SendGroupPausedMessage(programGroup);
    }

    private ValueTask LogIgnoredAfk()
    {
        _logger.LogInformation("Ignoring shock, user is AFK");
        if (string.IsNullOrEmpty(_moduleConfig.Config.Chatbox.IgnoredAfk))
            return ValueTask.CompletedTask;

        return _chatboxService.SendGenericMessage(_moduleConfig.Config.Chatbox.IgnoredAfk);
    }

    private async Task SenderLoopAsync()
    {
        while (_oscServerActive)
        {
            await _oscHandler.SendParams();
            await Task.Delay(300);
        }
    }

    private async Task<bool> ControlGroup(Guid groupId, ushort duration, byte intensity, ControlType type,
        bool exclusive = false)
    {
        if (groupId == Guid.Empty)
        {
            var controlCommandsAll = _openShockService.Data.Hubs.Value.SelectMany(x => x.Shockers)
                .Select(x => new ShockerControl
                {
                    Id = x.Id,
                    Duration = duration,
                    Intensity = intensity,
                    Type = type,
                    Exclusive = exclusive
                });
            await _openShockService.Control.Control(controlCommandsAll);
            return true;
        }

        if (!_moduleConfig.Config.Groups.TryGetValue(groupId, out var group)) return false;

        var controlCommands = group.Shockers.Select(x => new ShockerControl
        {
            Id = x,
            Duration = duration,
            Intensity = intensity,
            Type = type,
            Exclusive = exclusive
        });

        await _openShockService.Control.Control(controlCommands);
        return true;
    }

    private async Task SendCommand(ProgramGroup programGroup, ushort duration, byte intensity, ControlType type,
        bool exclusive = false)
    {
        // Intensity is pre scaled to 0 - 100
        var actualIntensity = programGroup.NextIntensity == 0 ? intensity : programGroup.NextIntensity;
        var actualDuration = programGroup.NextDuration == 0
            ? duration
            : GetScaledDuration(programGroup, programGroup.NextDuration);

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


        await ControlGroup(programGroup.Id, actualDuration, actualIntensity, type, exclusive);
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
        foreach (var (pos, programGroup) in _dataLayer.ProgramGroups)
        {
            await CheckProgramGroup(programGroup, pos);
        }
    }

    private void LiveControlGroupFrameCheckLoop(ProgramGroup group, byte intensity, ControlType type)
    {
        if (group.Id == Guid.Empty)
        {
            _openShockService.Control.ControlAllShockers(intensity, type);
            return;
        }

        if (group.ConfigGroup == null)
        {
            _logger.LogWarning("Group [{GroupId}] does not have a config group", group.Id);
            return;
        }

        _openShockService.Control.LiveControl(group.ConfigGroup.Shockers, intensity, type);
    }

    private async Task CheckProgramGroup(ProgramGroup programGroup, Guid pos)
    {
        var pass = CheckAndSetAllPreconditions(programGroup);
        
        # region Concurrent Handling

        if (programGroup.ConcurrentIntensity != 0 && pass.IsT0)
        {
            LiveControlGroupFrameCheckLoop(programGroup,
                GetScaledIntensity(programGroup, programGroup.ConcurrentIntensity), programGroup.ConcurrentType);
            programGroup.LastConcurrentIntensity = programGroup.ConcurrentIntensity;
            return;
        }

        // This means concurrent intensity is 0
        if (programGroup.LastConcurrentIntensity != 0)
        {
            LiveControlGroupFrameCheckLoop(programGroup, 0, ControlType.Stop);
            programGroup.LastConcurrentIntensity = 0;
        }

        # endregion

        // Physbone while held handling
        if (programGroup.TriggerMethod == TriggerMethod.None && programGroup.IsGrabbed)
        {
            if(!await HandlePrecondition(pass, programGroup)) return;
            
            var heldAction = _moduleConfig.Config.GetGroupOrGlobal(programGroup,
                behaviourConfig => behaviourConfig.WhileBoneHeld,
                group => group.OverrideBoneHeldAction);

            if (heldAction != BoneAction.None && (programGroup.PhysBoneGrabLimitTime == null ||
                                                  programGroup.PhysBoneGrabLimitTime > DateTime.UtcNow) &&
                programGroup.LastHeldAction < DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(100)))
            {
                var pullIntensityTranslated = GetPhysbonePullIntensity(programGroup, programGroup.LastStretchValue);
                programGroup.LastHeldAction = DateTime.UtcNow;

                _logger.LogDebug("Vibrating/Shocking {Shocker} at {Intensity}", pos, pullIntensityTranslated);

                LiveControlGroupFrameCheckLoop(programGroup, pullIntensityTranslated,
                    heldAction.ToControlType());
            }
        }
        
        // Regular touch trigger
        
        if (programGroup.TriggerMethod == TriggerMethod.None)
            return;

        if (programGroup.TriggerMethod == TriggerMethod.Manual &&
            programGroup.LastActive.AddMilliseconds(_moduleConfig.Config.Behaviour.HoldTime) > DateTime.UtcNow)
            return;
        
       if(!await HandlePrecondition(pass, programGroup)) return;


        SendCommand(programGroup, GetDuration(programGroup), GetIntensity(programGroup), ControlType.Shock, false);
    }

    private OneOf.OneOf<Success, KillSwitch, Cooldown, Paused, Afk> CheckAndSetAllPreconditions(ProgramGroup programGroup)
    {
        var configBehaviour = _moduleConfig.Config.Behaviour;

        if (_underscoreConfig.KillSwitch)
        {
            programGroup.TriggerMethod = TriggerMethod.None;
            return new KillSwitch();
        }

        if (programGroup.Paused)
        {
            programGroup.TriggerMethod = TriggerMethod.None;
            return new Paused();
        }

        if (_isAfk && configBehaviour.DisableWhileAfk)
        {
            programGroup.TriggerMethod = TriggerMethod.None;
            return new Afk();
        }
        
        var cooldownTime = configBehaviour.CooldownTime;
        if (programGroup.ConfigGroup is { OverrideCooldownTime: true })
            cooldownTime = programGroup.ConfigGroup.CooldownTime;

        var isActiveOrOnCooldown =
            programGroup.LastExecuted.AddMilliseconds(cooldownTime)
                .AddMilliseconds(programGroup.LastDuration) > DateTime.UtcNow;

        if (isActiveOrOnCooldown)
        {
            programGroup.TriggerMethod = TriggerMethod.None;
            return new Cooldown();
        }

        return new Success();
    }

    private ushort GetScaledDuration(ProgramGroup programGroup, float scale)
    {
        scale = MathUtils.Saturate(scale);

        if (programGroup.ConfigGroup is not { OverrideDuration: true })
        {
            // Use global config
            var config = _moduleConfig.Config.Behaviour;

            if (!config.RandomDuration) return (ushort)(config.FixedDuration * scale);
            var rdr = config.DurationRange;
            return (ushort)
                (MathUtils.LerpUShort(
                     (ushort)(rdr.Min / DurationStep), (ushort)(rdr.Max / DurationStep), scale)
                 * DurationStep);
        }

        // Use group config
        var groupConfig = programGroup.ConfigGroup;

        if (!groupConfig.RandomDuration) return (ushort)(groupConfig.FixedDuration * scale);
        var groupRdr = groupConfig.DurationRange;
        return (ushort)(MathUtils.LerpUShort((ushort)(groupRdr.Min / DurationStep),
            (ushort)(groupRdr.Max / DurationStep), scale) * DurationStep);
    }

    private byte GetScaledIntensity(ProgramGroup programGroup, byte intensity)
    {
        if (programGroup.ConfigGroup is not { OverrideIntensity: true })
        {
            // Use global config
            var config = _moduleConfig.Config.Behaviour;

            if (!config.RandomIntensity) return (byte)MathUtils.LerpFloat(0, config.FixedIntensity, intensity / 100f);
            return (byte)MathUtils.LerpFloat(config.IntensityRange.Min, config.IntensityRange.Max, intensity / 100f);
        }

        // Use group config
        var groupConfig = programGroup.ConfigGroup;

        if (!groupConfig.RandomIntensity)
            return (byte)MathUtils.LerpFloat(0, groupConfig.FixedIntensity, intensity / 100f);
        return (byte)MathUtils.LerpFloat(groupConfig.IntensityRange.Min, groupConfig.IntensityRange.Max,
            intensity / 100f);
    }

    private byte GetPhysbonePullIntensity(ProgramGroup programGroup, float stretch)
    {
        stretch = MathUtils.Saturate(stretch);
        if (programGroup.ConfigGroup is not { OverrideIntensity: true })
        {
            // Use global config
            var config = _moduleConfig.Config.Behaviour;

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
            var config = _moduleConfig.Config.Behaviour;

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
        if (programGroup.ConfigGroup is not { OverrideIntensity: true })
        {
            // Use global config
            var config = _moduleConfig.Config.Behaviour;

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

public struct KillSwitch;
public struct Cooldown;
public struct Paused;
public struct Afk;