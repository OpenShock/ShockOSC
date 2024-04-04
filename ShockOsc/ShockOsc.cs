using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using LucHeart.CoreOSC;
using OpenShock.SDK.CSharp.Live.Models;
using OpenShock.SDK.CSharp.Models;
using OpenShock.ShockOsc.Logging;
using OpenShock.ShockOsc.Models;
using OpenShock.ShockOsc.OscChangeTracker;
using OpenShock.ShockOsc.OscQueryLibrary;
using OpenShock.ShockOsc.Utils;
using Serilog;
using Serilog.Events;
using SmartFormat;

#pragma warning disable CS4014

namespace OpenShock.ShockOsc;

public static class ShockOsc
{
    private static ILogger _logger = null!;
    private static bool _oscServerActive;
    private static bool _isAfk;
    private static bool _isMuted;
    public static string AvatarId = string.Empty;
    private static readonly Random Random = new();
    public static readonly ConcurrentDictionary<string, Shocker> Shockers = new();

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

    public enum AuthState
    {
        NotAuthenticated,
        Authenticating,
        Authenticated
    }

    public static Dictionary<string, object?> ParamsInUse = new();
    public static Dictionary<string, object?> AllAvatarParams = new();

    public static Action<bool>? OnParamsChange;
    public static Action? OnConfigUpdate;
    public static Action<AuthState>? SetAuthLoading;
    public static AuthState CurrentAuthState = AuthState.NotAuthenticated;

    public static async Task StartMain()
    {
        
        _logger = Log.ForContext(typeof(ShockOsc));
        
        _logger.Information("Starting ShockOsc version {Version}",
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "error");

        ConnectToHub();

        _logger.Information("Creating OSC Query Server...");
        _ = new OscQueryServer(
            "ShockOsc", // service name
            "127.0.0.1", // ip address for udp and http server
            FoundVrcClient, // optional callback on vrc discovery
            OnAvatarChange // optional parameter list callback on vrc discovery
        );

        // listen for VRC on every network interface
        if (Config.ConfigInstance.Osc.QuestSupport)
        {
            var host = await Dns.GetHostEntryAsync(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    continue;

                var ipAddress = ip.ToString();
                _ = new OscQueryServer(
                    "ShockOsc", // service name
                    ipAddress, // ip address for udp and http server
                    FoundVrcClient, // optional callback on vrc discovery
                    OnAvatarChange // parameter list callback on vrc discovery
                );
            }
        }

        await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
    }

    private static void ConnectToHub()
    {
        _logger.Information("Init user hub...");
        SetAuthSate(AuthState.NotAuthenticated);
        if (string.IsNullOrEmpty(Config.ConfigInstance.OpenShock.Token))
            return;

        SetAuthSate(AuthState.Authenticating);
        UserHubClient.SetupLiveClient().ContinueWith(task =>
        {
            if (task.IsFaulted)
                SetAuthSate(AuthState.NotAuthenticated);

            if (task.IsCompletedSuccessfully)
            {
                OpenShockApi.GetShockers();
                SetAuthSate(AuthState.Authenticated);
            }
        });
    }

    public static void Logout()
    {
        Config.ConfigInstance.OpenShock.Token = string.Empty;
        Config.Save();
        UserHubClient.Disconnect();
        SetAuthSate(AuthState.NotAuthenticated);
    }

    public static void ClickLogin()
    {
        Config.Save();
        _logger.Information("Clicking login");
        ConnectToHub();
    }

    public static void SetAuthSate(AuthState state)
    {
        CurrentAuthState = state;
        SetAuthLoading?.Invoke(state);
    }

    private static void OnParamChange(bool shockOscParam)
    {
        OnParamsChange?.Invoke(shockOscParam);
    }

    private static void FoundVrcClient()
    {
        _logger.Information("Found VRC client");
        // stop tasks
        _oscServerActive = false;
        Task.Delay(1000).Wait(); // wait for tasks to stop

        OscClient.CreateGameConnection(IPAddress.Parse(OscQueryServer.OscIpAddress), OscQueryServer.OscReceivePort, OscQueryServer.OscSendPort);
        _logger.Information("Connecting UDP Clients...");

        // Start tasks
        _oscServerActive = true;
        OsTask.Run(ReceiverLoopAsync);
        OsTask.Run(SenderLoopAsync);
        OsTask.Run(CheckLoop);

        _logger.Information("Ready");
        OsTask.Run(UnderscoreConfig.SendUpdateForAll);
    }
    
    public static void RefreshShockers()
    {
        Shockers.Clear();
        Shockers.TryAdd("_All", new Shocker(Guid.Empty, "_All"));
        // foreach (var (shockerName, shocker) in Config.ConfigInstance.OpenShock.Shockers)
        // {
        //     if(!shocker.Enabled) continue;
        //     
        //     if (string.IsNullOrEmpty(shocker.NickName))
        //         Shockers.TryAdd(shockerName, new Shocker(shocker.Id, shockerName));
        //     else
        //         Shockers.TryAdd(shocker.NickName, new Shocker(shocker.Id, shocker.NickName));
        // }
    }

    public static void SaveShockers()
    {
        RefreshShockers();
        Config.Save();
    }

    private static void OnAvatarChange(Dictionary<string, object?>? parameters, string avatarId)
    {
        AvatarId = avatarId;
        try
        {
            foreach (var obj in Shockers)
            {
                obj.Value.Reset();
            }

            var parameterCount = 0;

            if (parameters == null)
            {
                _logger.Error("Failed to receive avatar parameters");
                return;
            }

            ParamsInUse.Clear();
            AllAvatarParams.Clear();

            foreach (var param in parameters.Keys)
            {
                if (param.StartsWith("/avatar/parameters/"))
                    AllAvatarParams.TryAdd(param[19..], parameters[param]);

                if (!param.StartsWith("/avatar/parameters/ShockOsc/"))
                    continue;

                var paramName = param[28..];
                var lastUnderscoreIndex = paramName.LastIndexOf('_') + 1;
                var action = string.Empty;
                var shockerName = paramName;
                if (lastUnderscoreIndex > 1)
                {
                    shockerName = paramName[..(lastUnderscoreIndex - 1)];
                    action = paramName.Substring(lastUnderscoreIndex, paramName.Length - lastUnderscoreIndex);
                }

                if (ShockerParams.Contains(action))
                {
                    parameterCount++;
                    ParamsInUse.TryAdd(paramName, parameters[param]);
                }

                if (!Shockers.ContainsKey(shockerName) && !shockerName.StartsWith("_"))
                {
                    _logger.Warning("Unknown shocker on avatar {Shocker}", shockerName);
                    _logger.Debug("Param: {Param}", param);
                }
            }

            _logger.Information("Loaded avatar config with {ParamCount} parameters", parameterCount);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error on avatar change logic");
        }
        OnParamChange(true);
    }

    private static async Task ReceiverLoopAsync()
    {
        while (_oscServerActive)
        {
            try
            {
                await ReceiveLogic();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error in receiver loop");
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private static async Task ReceiveLogic()
    {
        OscMessage received;
        try
        {
            received = await OscClient.ReceiveGameMessage()!;
        }
        catch (Exception e)
        {
            _logger.Verbose(e, "Error receiving message");
            return;
        }

        var addr = received.Address;
        _logger.Verbose("Received message: {Addr}", addr);

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
                _logger.Debug("Avatar changed: {AvatarId}", avatarId);
                OsTask.Run(OscQueryServer.GetParameters);
                OsTask.Run(UnderscoreConfig.SendUpdateForAll);
                return;
            case "/avatar/parameters/AFK":
                _isAfk = received.Arguments.ElementAtOrDefault(0) is true;
                _logger.Debug("Afk: {State}", _isAfk);
                return;
            case "/avatar/parameters/MuteSelf":
                _isMuted = received.Arguments.ElementAtOrDefault(0) is true;
                _logger.Debug("Muted: {State}", _isMuted);
                return;
        }

        if (!addr.StartsWith("/avatar/parameters/ShockOsc/"))
            return;

        var pos = addr.Substring(28, addr.Length - 28);

        // Check if _Config
        if (pos.StartsWith("_Config/"))
        {
            UnderscoreConfig.HandleCommand(pos, received.Arguments);
            return;
        }

        var lastUnderscoreIndex = pos.LastIndexOf('_') + 1;
        var action = string.Empty;
        var shockerName = pos;
        if (lastUnderscoreIndex > 1)
        {
            shockerName = pos[..(lastUnderscoreIndex - 1)];
            action = pos.Substring(lastUnderscoreIndex, pos.Length - lastUnderscoreIndex);
        }

        if (ParamsInUse.ContainsKey(pos))
        {
            ParamsInUse[pos] = received.Arguments[0];
            OnParamChange(true);
        }
        else
            ParamsInUse.TryAdd(pos, received.Arguments[0]);

        if (!ShockerParams.Contains(action)) return;

        if (!Shockers.ContainsKey(shockerName))
        {
            if (shockerName == "_Any") return;
            _logger.Warning("Unknown shocker {Shocker}", shockerName);
            _logger.Debug("Param: {Param}", pos);
            return;
        }

        var shocker = Shockers[shockerName];

        var value = received.Arguments.ElementAtOrDefault(0);
        switch (action)
        {
            case "IShock":
                // TODO: check Cooldowns
                if (value is not true) return;
                if (UnderscoreConfig.KillSwitch)
                {
                    shocker.TriggerMethod = TriggerMethod.None;
                    await LogIgnoredKillSwitchActive();
                    return;
                }

                if (_isAfk && Config.ConfigInstance.Behaviour.DisableWhileAfk)
                {
                    shocker.TriggerMethod = TriggerMethod.None;
                    await LogIgnoredAfk();
                    return;
                }

                OsTask.Run(() => InstantShock(shocker, GetDuration(), GetIntensity()));

                return;
            case "Stretch":
                if (value is float stretch)
                    shocker.LastStretchValue = stretch;
                return;
            case "IsGrabbed":
                var isGrabbed = value is true;
                if (shocker.IsGrabbed && !isGrabbed)
                {
                    // on physbone release
                    if (shocker.LastStretchValue != 0)
                    {
                        shocker.TriggerMethod = TriggerMethod.PhysBoneRelease;
                        shocker.LastActive = DateTime.UtcNow;
                    }
                    else if (Config.ConfigInstance.Behaviour.WhileBoneHeld !=
                                Config.Conf.BehaviourConf.BoneHeldAction.None)
                    {
                        await CancelAction(shocker);
                    }
                }

                shocker.IsGrabbed = isGrabbed;
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
            shocker.TriggerMethod = TriggerMethod.Manual;
            shocker.LastActive = DateTime.UtcNow;
        }
        else shocker.TriggerMethod = TriggerMethod.None;
    }

    private static ValueTask LogIgnoredKillSwitchActive()
    {
        _logger.Information("Ignoring shock, kill switch is active");
        if (string.IsNullOrEmpty(Config.ConfigInstance.Chatbox.IgnoredKillSwitchActive)) return ValueTask.CompletedTask;

        return OscClient.SendChatboxMessage(
            $"{Config.ConfigInstance.Chatbox.Prefix}{Config.ConfigInstance.Chatbox.IgnoredKillSwitchActive}");
    }

    private static ValueTask LogIgnoredAfk()
    {
        _logger.Information("Ignoring shock, user is AFK");
        if (string.IsNullOrEmpty(Config.ConfigInstance.Chatbox.IgnoredAfk)) return ValueTask.CompletedTask;

        return OscClient.SendChatboxMessage(
            $"{Config.ConfigInstance.Chatbox.Prefix}{Config.ConfigInstance.Chatbox.IgnoredAfk}");
    }

    private static async Task SenderLoopAsync()
    {
        while (_oscServerActive)
        {
            await SendParams();
            await Task.Delay(300);
        }
    }

    private static readonly ChangeTrackedOscParam<bool> ParamAnyActive = new("_Any", "_Active", false);
    private static readonly ChangeTrackedOscParam<bool> ParamAnyCooldown = new("_Any", "_Cooldown", false);
    private static readonly ChangeTrackedOscParam<float> ParamAnyCooldownPercentage = new("_Any", "_CooldownPercentage", 0f);
    private static readonly ChangeTrackedOscParam<float> ParamAnyIntensity = new("_Any", "_Intensity", 0f);

    private static async Task InstantShock(Shocker shocker, uint duration, byte intensity)
    {
        shocker.LastExecuted = DateTime.UtcNow;
        shocker.LastDuration = duration;
        var intensityPercentage = Math.Round(GetFloatScaled(intensity) * 100f);
        shocker.LastIntensity = intensity;

        ForceUnmute();
        SendParams();

        shocker.TriggerMethod = TriggerMethod.None;
        var inSeconds = MathF.Round(duration / 1000f, 1).ToString(CultureInfo.InvariantCulture);
        _logger.Information(
            "Sending shock to {Shocker} Intensity: {Intensity} IntensityPercentage: {IntensityPercentage}% Length:{Length}s",
            shocker.Name, intensity, intensityPercentage, inSeconds);

        await ControlShocker(shocker.Id, duration, intensity, ControlType.Shock);

        if (!Config.ConfigInstance.Osc.Chatbox) return;
        // Chatbox message local
        var dat = new
        {
            ShockerName = shocker.Name,
            Intensity = intensity,
            IntensityPercentage = intensityPercentage,
            Duration = duration,
            DurationSeconds = inSeconds
        };
        var template = Config.ConfigInstance.Chatbox.Types[ControlType.Shock];
        var msg = $"{Config.ConfigInstance.Chatbox.Prefix}{Smart.Format(template.Local, dat)}";
        await OscClient.SendChatboxMessage(msg);
    }

    /// <summary>
    /// Coverts to a 0-1 float and scale it to the max intensity
    /// </summary>
    /// <param name="intensity"></param>
    /// <returns></returns>
    private static float GetFloatScaled(byte intensity) =>
        ClampFloat((float)intensity / Config.ConfigInstance.Behaviour.IntensityRange.Max);

    private static async Task SendParams()
    {
        // TODO: maybe force resend on avatar change
        var anyActive = false;
        var anyCooldown = false;
        var anyCooldownPercentage = 0f;
        var anyIntensity = 0f;

        foreach (var shocker in Shockers.Values)
        {
            var isActive = shocker.LastExecuted.AddMilliseconds(shocker.LastDuration) > DateTime.UtcNow;
            var isActiveOrOnCooldown =
                shocker.LastExecuted.AddMilliseconds(Config.ConfigInstance.Behaviour.CooldownTime)
                    .AddMilliseconds(shocker.LastDuration) > DateTime.UtcNow;
            if (!isActiveOrOnCooldown && shocker.LastIntensity > 0)
                shocker.LastIntensity = 0;

            var onCoolDown = !isActive && isActiveOrOnCooldown;

            var cooldownPercentage = 0f;
            if (onCoolDown)
                cooldownPercentage = ClampFloat(1 -
                                                (float)(DateTime.UtcNow -
                                                        shocker.LastExecuted.AddMilliseconds(shocker.LastDuration))
                                                .TotalMilliseconds / Config.ConfigInstance.Behaviour.CooldownTime);

            await shocker.ParamActive.SetValue(isActive);
            await shocker.ParamCooldown.SetValue(onCoolDown);
            await shocker.ParamCooldownPercentage.SetValue(cooldownPercentage);
            await shocker.ParamIntensity.SetValue(GetFloatScaled(shocker.LastIntensity));

            if (isActive) anyActive = true;
            if (onCoolDown) anyCooldown = true;
            anyCooldownPercentage = Math.Max(anyCooldownPercentage, cooldownPercentage);
            anyIntensity = Math.Max(anyIntensity, GetFloatScaled(shocker.LastIntensity));
        }

        await ParamAnyActive.SetValue(anyActive);
        await ParamAnyCooldown.SetValue(anyCooldown);
        await ParamAnyCooldownPercentage.SetValue(anyCooldownPercentage);
        await ParamAnyIntensity.SetValue(anyIntensity);
    }

    private static async Task CheckLoop()
    {
        while (_oscServerActive)
        {
            try
            {
                await CheckLogic();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error in check loop");
            }

            await Task.Delay(20);
        }
    }

    private static byte GetIntensity()
    {
        var config = Config.ConfigInstance.Behaviour;

        if (!config.RandomIntensity) return config.FixedIntensity;
        var rir = config.IntensityRange;
        var intensityValue = Random.Next((int)rir.Min, (int)rir.Max);
        return (byte)intensityValue;
    }

    private static async Task CheckLogic()
    {
        var config = Config.ConfigInstance.Behaviour;
        foreach (var (pos, shocker) in Shockers)
        {
            var isActiveOrOnCooldown =
                shocker.LastExecuted.AddMilliseconds(Config.ConfigInstance.Behaviour.CooldownTime)
                    .AddMilliseconds(shocker.LastDuration) > DateTime.UtcNow;

            if (shocker.TriggerMethod == TriggerMethod.None &&
                Config.ConfigInstance.Behaviour.WhileBoneHeld != Config.Conf.BehaviourConf.BoneHeldAction.None &&
                !isActiveOrOnCooldown &&
                shocker.IsGrabbed &&
                shocker.LastVibration < DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(300)))
            {
                var vibrationIntensity = shocker.LastStretchValue * 100f;
                if (vibrationIntensity < 1)
                    vibrationIntensity = 1;
                shocker.LastVibration = DateTime.UtcNow;

                _logger.Debug("Vibrating {Shocker} at {Intensity}", pos, vibrationIntensity);
                await ControlShocker(shocker.Id, 1000, (byte)vibrationIntensity,
                    Config.ConfigInstance.Behaviour.WhileBoneHeld == Config.Conf.BehaviourConf.BoneHeldAction.Shock
                        ? ControlType.Shock
                        : ControlType.Vibrate);
            }

            if (shocker.TriggerMethod == TriggerMethod.None)
                continue;

            if (shocker.TriggerMethod == TriggerMethod.Manual &&
                shocker.LastActive.AddMilliseconds(config.HoldTime) > DateTime.UtcNow)
                continue;

            if (isActiveOrOnCooldown)
            {
                shocker.TriggerMethod = TriggerMethod.None;
                _logger.Information("Ignoring shock {Shocker} is on cooldown", pos);
                continue;
            }

            if (UnderscoreConfig.KillSwitch)
            {
                shocker.TriggerMethod = TriggerMethod.None;
                await LogIgnoredKillSwitchActive();
                continue;
            }

            if (_isAfk && config.DisableWhileAfk)
            {
                shocker.TriggerMethod = TriggerMethod.None;
                await LogIgnoredAfk();
                continue;
            }

            byte intensity;

            if (shocker.TriggerMethod == TriggerMethod.PhysBoneRelease)
            {
                intensity = (byte)LerpFloat(config.IntensityRange.Min, config.IntensityRange.Max,
                    shocker.LastStretchValue);
                shocker.LastStretchValue = 0;
            }
            else intensity = GetIntensity();

            InstantShock(shocker, GetDuration(), intensity);
        }
    }

    private static uint GetDuration()
    {
        var config = Config.ConfigInstance.Behaviour;

        if (!config.RandomDuration) return config.FixedDuration;
        var rdr = config.DurationRange;
        return (uint)(Random.Next((int)(rdr.Min / config.RandomDurationStep),
            (int)(rdr.Max / config.RandomDurationStep)) * config.RandomDurationStep);
    }

    private static Task ControlShocker(Guid shockerId, uint duration, byte intensity, ControlType type)
    {
        if (shockerId == Guid.Empty)
            return UserHubClient.Control(Shockers.Where(x => x.Value.Id != Guid.Empty).Select(x => new Control
            {
                Id = x.Value.Id,
                Intensity = intensity,
                Duration = duration,
                Type = type
            }).ToArray());

        return UserHubClient.Control(new Control
        {
            Id = shockerId,
            Intensity = intensity,
            Duration = duration,
            Type = type
        });
    }

    public static async Task RemoteActivateShocker(ControlLogSender sender, ControlLog log)
    {
        if (sender.ConnectionId == UserHubClient.ConnectionId)
        {
            _logger.Debug("Ignoring remote command log cause it was the local connection");
            return;
        }

        var inSeconds = ((float)log.Duration / 1000).ToString(CultureInfo.InvariantCulture);

        if (sender.CustomName == null)
            _logger.Information(
                "Received remote {Type} for \"{ShockerName}\" at {Intensity}%:{Duration}s by {Sender}",
                log.Type, log.Shocker.Name, log.Intensity, inSeconds, sender.Name);
        else
            _logger.Information(
                "Received remote {Type} for \"{ShockerName}\" at {Intensity}%:{Duration}s by {SenderCustomName} [{Sender}]",
                log.Type, log.Shocker.Name, log.Intensity, inSeconds, sender.CustomName, sender.Name);

        var template = Config.ConfigInstance.Chatbox.Types[log.Type];
        if (Config.ConfigInstance.Osc.Chatbox && Config.ConfigInstance.Chatbox.DisplayRemoteControl && template.Enabled)
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
                $"{Config.ConfigInstance.Chatbox.Prefix}{Smart.Format(sender.CustomName == null ? template.Remote : template.RemoteWithCustomName, dat)}";
            await OscClient.SendChatboxMessage(msg);
        }

        var shocker = Shockers.Values.Where(s => s.Id == log.Shocker.Id).ToArray();
        if (shocker.Length <= 0)
            return;

        var oneShock = false;

        foreach (var pain in shocker)
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
                    _logger.Error("ControlType was out of range. Value was: {Type}", log.Type);
                    break;
            }

            if (oneShock)
            {
                ForceUnmute();
                SendParams();
            }
        }
    }

    private static async Task ForceUnmute()
    {
        if (!Config.ConfigInstance.Behaviour.ForceUnmute || !_isMuted) return;
        _logger.Debug("Force unmuting...");
        await OscClient.SendGameMessage("/input/Voice", false);
        await Task.Delay(50);
        await OscClient.SendGameMessage("/input/Voice", true);
        await Task.Delay(50);
        await OscClient.SendGameMessage("/input/Voice", false);
    }

    private static Task CancelAction(Shocker shocker)
    {
        _logger.Debug("Cancelling action");
        return ControlShocker(shocker.Id, 0, 0, ControlType.Stop);
    }

    private static float LerpFloat(float min, float max, float t) => min + (max - min) * t;
    public static float ClampFloat(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    public static uint LerpUint(uint min, uint max, float t) => (uint)(min + (max - min) * t);
    public static uint ClampUint(uint value, uint min, uint max) => value < min ? min : value > max ? max : value;
}
