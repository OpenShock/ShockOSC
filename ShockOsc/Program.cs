using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using CoreOSC;
using CoreOSC.IO;
using Serilog;
using Serilog.Events;

namespace ShockLink.ShockOsc;

public static class Program
{
    private enum TriggerMethod
    {
        None,
        Manual,
        PhysBoneRelease
    }
    
    private class Shocker
    {
        public DateTime LastActive { get; set; }
        public DateTime LastExecuted { get; set; }
        public float LastStretchValue { get; set; }
        public bool IsGrabbed { get; set; }
        public bool HasCooldownParam { get; set; }
        public TriggerMethod TriggerMethod { get; set; }
    }

    private static bool _isAfk;
    private static bool _isMuted;
    private static readonly ConcurrentDictionary<string, Shocker> Shockers = new();
    private static readonly Random Random = new();

    private static readonly UdpClient ReceiverClient = new((int)Config.ConfigInstance.Osc.ReceivePort);
    private static readonly UdpClient SenderClient =
        new(new IPEndPoint(IPAddress.Loopback, 0));

    private static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Filter.ByExcluding(ev => ev.Exception is InvalidDataException a && a.Message.StartsWith("Invocation provides"))
            .WriteTo.Console(LogEventLevel.Information, "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Starting ShockLink.ShockOsc version {Version}",
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "error");
        Log.Information("Found shockers: {Shockers}", Config.ConfigInstance.ShockLink.Shockers.Select(x => x.Key));

        Log.Information("Init user hub...");
        await UserHubClient.InitializeAsync();

        Log.Information("Connecting UDP Clients...");
        SenderClient.Connect(IPAddress.Loopback, (int)Config.ConfigInstance.Osc.SendPort);

        // Start tasks
#pragma warning disable CS4014
        SlTask.Run(ReceiverLoopAsync);
        SlTask.Run(SenderLoopAsync);
        SlTask.Run(CheckLoop);
#pragma warning restore CS4014

        Log.Information("Ready");
        await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
    }

    private static async Task ReceiverLoopAsync()
    {
        while (true) await ReceiveLogic();
    }

    private static async Task ReceiveLogic()
    {
        var received = await ReceiverClient.ReceiveMessageAsync();
        var addr = received.Address.Value;
        Log.Verbose("Received message: {Addr}", addr);
        
        switch (addr)
        {
            case "/avatar/change":
                Shockers.Clear();
                Log.Debug("Clearing shockers");
                return;
            case "/avatar/parameters/AFK":
                _isAfk = received.Arguments.ElementAtOrDefault(0) is OscTrue;
                Log.Debug("Afk: {State}", _isAfk);
                return;
            case "/avatar/parameters/MuteSelf":
                _isMuted = received.Arguments.ElementAtOrDefault(0) is OscTrue;
                Log.Debug("Muted: {State}", _isMuted);
                return;
        }

        if (!addr.StartsWith("/avatar/parameters/ShockOsc"))
            return;

        var pos = addr.Substring(28, addr.Length - 28);
        var lastUnderscoreIndex = pos.LastIndexOf('_') + 1;
        var action = string.Empty;
        if (lastUnderscoreIndex != 0)
            action = pos.Substring(lastUnderscoreIndex, pos.Length - lastUnderscoreIndex);

        var shockerName = pos;
        if (action is "Stretch" or "IsGrabbed" or "IsPosed" or "Angle" or "Squish" or "Cooldown")
            shockerName = pos.Substring(0, lastUnderscoreIndex - 1);

        if (!Config.ConfigInstance.ShockLink.Shockers.ContainsKey(shockerName))
        {
            Log.Warning("Unknown shocker {Shocker}", shockerName);
            return;
        }

        if (!Shockers.ContainsKey(shockerName))
            Shockers.TryAdd(shockerName, new Shocker());

        var value = received.Arguments.ElementAtOrDefault(0);
        switch (action)
        {
            case "Stretch":
                if (value is float stretch)
                    Shockers[shockerName].LastStretchValue = stretch;
                return;

            case "IsGrabbed":
                var isGrabbed = value is OscTrue;
                if (Shockers[shockerName].IsGrabbed && !isGrabbed && Shockers[shockerName].LastStretchValue != 0)
                {
                    Shockers[shockerName].TriggerMethod = TriggerMethod.PhysBoneRelease;
                    Shockers[shockerName].LastActive = DateTime.UtcNow;
                }
                
                Shockers[shockerName].IsGrabbed = isGrabbed;
                return;
            
            case "Cooldown":
                Shockers[shockerName].HasCooldownParam = true;
                return;
            
            case "":
                break;

            default:
                return;
        }

        if (value is OscTrue)
        {
            Shockers[shockerName].TriggerMethod = TriggerMethod.Manual;
            Shockers[shockerName].LastActive = DateTime.UtcNow;
        }
        else Shockers[shockerName].TriggerMethod = TriggerMethod.None;
    }
        
    private static async Task SenderLoopAsync()
    {
        while (true)
        {
            await SendLogic();
            await Task.Delay(300);
        }
    }

    private static async Task SendLogic()
    {
        foreach (var shocker in Shockers.Values)
        {
            if (!shocker.HasCooldownParam)
                continue;
            
            var onCoolDown = shocker.LastExecuted > DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(Config.ConfigInstance.Behaviour.CooldownTime));
            await SenderClient.SendMessageAsync(new OscMessage(new Address($"/avatar/parameters/ShockOsc/{shocker}_Cooldown"),
                new object[] { onCoolDown ? OscTrue.True : OscFalse.False }));
        }
    }

    private static async Task CheckLoop()
    {
        while (true)
        {
            await CheckLogic();
            await Task.Delay(20);
        }
    }

    private static async Task CheckLogic()
    {
        foreach (var (pos, shocker) in Shockers)
        {
            if (shocker.TriggerMethod == TriggerMethod.None)
                continue;
            
            if (shocker.TriggerMethod == TriggerMethod.Manual && shocker.LastActive.AddMilliseconds(Config.ConfigInstance.Behaviour.HoldTime) > DateTime.UtcNow)
                continue;
            
            if (shocker.LastExecuted >
                DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(Config.ConfigInstance.Behaviour.CooldownTime)))
            {
                shocker.TriggerMethod = TriggerMethod.None;
                Log.Information("Ignoring shock {Shocker} is on cooldown", pos);
                continue;
            }
            
            if (_isAfk && Config.ConfigInstance.Behaviour.DisableWhileAfk)
            {
                shocker.TriggerMethod = TriggerMethod.None;
                Log.Information("Ignoring shock {Shocker} user is AFK", pos);
                continue;
            }
            
            shocker.LastExecuted = DateTime.UtcNow;

            byte intensity = 0;
            uint duration;

            var beh = Config.ConfigInstance.Behaviour;
            if (beh.RandomDuration)
            {
                var rdr = beh.DurationRange;
                duration = (uint)(Random.Next((int)(rdr.Min / beh.RandomDurationStep), (int)(rdr.Max / beh.RandomDurationStep)) * beh.RandomDurationStep);
            }
            else duration = beh.FixedDuration;

            if (shocker.TriggerMethod == TriggerMethod.Manual)
            {
                if (beh.RandomIntensity)
                {
                    var rir = beh.IntensityRange;
                    intensity = (byte)Random.Next((int)rir.Min, (int)rir.Max);
                }
                else intensity = beh.FixedIntensity;
            }

            if (shocker.TriggerMethod == TriggerMethod.PhysBoneRelease)
            {
                var rir = beh.IntensityRange;
                intensity = (byte)LerpFloat(rir.Min, rir.Max, shocker.LastStretchValue);
                shocker.LastStretchValue = 0;
            }

            if (Config.ConfigInstance.Behaviour.ForceUnmute && _isMuted)
            {
                Log.Information("Force unmuting..");
                await SenderClient.SendMessageAsync(new OscMessage(new Address("/input/Voice"), new object[] { OscFalse.False }));
                await Task.Delay(50);
                await SenderClient.SendMessageAsync(new OscMessage(new Address("/input/Voice"), new object[] { OscTrue.True }));
                await Task.Delay(50);
                await SenderClient.SendMessageAsync(new OscMessage(new Address("/input/Voice"), new object[] { OscFalse.False }));
            }
            
            shocker.TriggerMethod = TriggerMethod.None;
            var inSeconds = ((float)duration / 1000).ToString(CultureInfo.InvariantCulture);
            Log.Information("Sending shock to {Shocker} strength:{Intensity} length:{Length}s", pos, intensity, inSeconds);

            var code = Config.ConfigInstance.ShockLink.Shockers[pos];
            await UserHubClient.Control(new Control
            {
                Id = code,
                Intensity = intensity,
                Duration = duration,
                Type = ControlType.Shock
            });

            if (!Config.ConfigInstance.Osc.Chatbox) continue;
            var msg = $"[ShockOsc] \"{pos}\" {intensity}:{inSeconds}s       ";
            await SenderClient.SendMessageAsync(Config.ConfigInstance.Osc.Hoscy
                ? new OscMessage(new Address("/hoscy/message"), new[] { msg })
                : new OscMessage(new Address("/chatbox/input"), new object[] { msg, OscTrue.True }));
        }
    }

    private static float LerpFloat(float min, float max, float t) => min + (max - min) * t;
}