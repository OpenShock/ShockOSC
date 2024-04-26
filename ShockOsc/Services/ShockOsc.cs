using System.Globalization;
using System.Net;
using LucHeart.CoreOSC;
using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Models;
using OpenShock.SDK.CSharp.Utils;
using OpenShock.ShockOsc.Backend;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Models;
using OpenShock.ShockOsc.OscQueryLibrary;
using OpenShock.ShockOsc.Utils;
using SmartFormat;

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


    public ShockOsc(ILogger<ShockOsc> logger,
        OscClient oscClient,
        BackendHubManager backendHubManager,
        UnderscoreConfig underscoreConfig,
        ConfigManager configManager,
        OscQueryServer oscQueryServer,
        ShockOscData dataLayer,
        OscHandler oscHandler, LiveControlManager liveControlManager)
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
        _dataLayer.ProgramGroups[Guid.Empty] = new ProgramGroup(Guid.Empty, "_All", _oscClient, null);
        foreach (var (id, group) in _configManager.Config.Groups) _dataLayer.ProgramGroups[id] = new ProgramGroup(id, group.Name, _oscClient, group);
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
                if(programGroup.ConfigGroup is { OverrideCooldownTime: true }) 
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

                OsTask.Run(() => InstantShock(programGroup, GetDuration(programGroup), GetIntensity(programGroup)));

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
            await _oscHandler.SendParams();
            await Task.Delay(300);
        }
    }

    private async Task InstantShock(ProgramGroup programGroup, uint duration, byte intensity, bool exclusive = false)
    {
        programGroup.LastExecuted = DateTime.UtcNow;
        programGroup.LastDuration = duration;
        var intensityPercentage = Math.Round(MathUtils.ClampFloat(intensity) * 100f);
        programGroup.LastIntensity = intensity;

        _oscHandler.ForceUnmute();
        _oscHandler.SendParams();

        programGroup.TriggerMethod = TriggerMethod.None;
        var inSeconds = MathF.Round(duration / 1000f, 1).ToString(CultureInfo.InvariantCulture);
        _logger.LogInformation(
            "Sending shock to {GroupName} Intensity: {Intensity} IntensityPercentage: {IntensityPercentage}% Length:{Length}s Exclusive: {Exclusive}",
            programGroup.Name, intensity, intensityPercentage, inSeconds, exclusive);

        await _backendHubManager.ControlGroup(programGroup.Id, duration, intensity, ControlType.Shock, exclusive);

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

    private byte GetIntensity(ProgramGroup programGroup)
    {
        if (programGroup.ConfigGroup is not { OverrideDuration: true })
        {
            // Use global config
            var config = _configManager.Config.Behaviour;

            if (!config.RandomIntensity) return config.FixedIntensity;
            var rir = config.IntensityRange;
            var intensityValue = Random.Next((int)rir.Min, (int)rir.Max);
            return (byte)intensityValue;
        }
        
        // Use groupConfig
        var groupConfig = programGroup.ConfigGroup;

        if (!groupConfig.RandomIntensity) return groupConfig.FixedIntensity;
        var groupRir = groupConfig.IntensityRange;
        var groupIntensityValue = Random.Next((int)groupRir.Min, (int)groupRir.Max);
        return (byte)groupIntensityValue;
    }

    private async Task CheckLogic()
    {
        var config = _configManager.Config.Behaviour;
        foreach (var (pos, programGroup) in _dataLayer.ProgramGroups)
        {
            var cooldownTime = _configManager.Config.Behaviour.CooldownTime;
            if(programGroup.ConfigGroup is { OverrideCooldownTime: true }) 
                cooldownTime = programGroup.ConfigGroup.CooldownTime;
            
            var isActiveOrOnCooldown =
                programGroup.LastExecuted.AddMilliseconds(cooldownTime)
                    .AddMilliseconds(programGroup.LastDuration) > DateTime.UtcNow;

            if (programGroup.TriggerMethod == TriggerMethod.None &&
                _configManager.Config.Behaviour.WhileBoneHeld !=
                BehaviourConf.BoneHeldAction.None &&
                !isActiveOrOnCooldown &&
                programGroup.IsGrabbed &&
                programGroup.LastVibration < DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(100)))
            {
                var vibrationIntensity = programGroup.LastStretchValue * 100f;
                if (vibrationIntensity < 1)
                    vibrationIntensity = 1;
                programGroup.LastVibration = DateTime.UtcNow;

                _logger.LogDebug("Vibrating {Shocker} at {Intensity}", pos, vibrationIntensity);

                await _liveControlManager.ControlGroupFrame(programGroup.Id, vibrationIntensity);
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
            var exclusive = false;
            
            if (programGroup.TriggerMethod == TriggerMethod.PhysBoneRelease)
            {
                intensity = programGroup.ConfigGroup is { OverrideIntensity: true }
                    ? (byte)MathUtils.LerpFloat(programGroup.ConfigGroup.IntensityRange.Min,
                        programGroup.ConfigGroup.IntensityRange.Max,
                        programGroup.LastStretchValue)
                    : (byte)MathUtils.LerpFloat(config.IntensityRange.Min, config.IntensityRange.Max,
                        programGroup.LastStretchValue);
                programGroup.LastStretchValue = 0;

                exclusive = true;
            }
            else intensity = GetIntensity(programGroup);

            InstantShock(programGroup, GetDuration(programGroup), intensity, exclusive);
        }
    }

    private uint GetDuration(ProgramGroup programGroup)
    {
        if (programGroup.ConfigGroup is not { OverrideDuration: true })
        {
            // Use global config
            var config = _configManager.Config.Behaviour;

            if (!config.RandomDuration) return config.FixedDuration;
            var rdr = config.DurationRange;
            return (uint)(Random.Next((int)(rdr.Min / config.RandomDurationStep),
                (int)(rdr.Max / config.RandomDurationStep)) * config.RandomDurationStep);
        }
        
        // Use group config
        var groupConfig = programGroup.ConfigGroup;

        if (!groupConfig.RandomDuration) return groupConfig.FixedDuration;
        var groupRdr = groupConfig.DurationRange;
        return (uint)(Random.Next((int)(groupRdr.Min / groupConfig.RandomDurationStep), 
            (int)(groupRdr.Max / groupConfig.RandomDurationStep)) * groupConfig.RandomDurationStep);
    }

}