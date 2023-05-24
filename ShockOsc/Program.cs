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
        public DateTime lastActive;
        public DateTime lastExecuted;
        public float lastStretchValue;
        public bool isGrabbed;
        public bool hasCooldownParam;
        public TriggerMethod triggerMethod;
    }

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

        if (!addr.StartsWith("/avatar/parameters/ShockOsc")) return;

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
                    Shockers[shockerName].lastStretchValue = stretch;
                return;

            case "IsGrabbed":
                var isGrabbed = value is OscTrue;
                if (Shockers[shockerName].isGrabbed && !isGrabbed && Shockers[shockerName].lastStretchValue != 0)
                {
                    Shockers[shockerName].triggerMethod = TriggerMethod.PhysBoneRelease;
                    Shockers[shockerName].lastActive = DateTime.UtcNow;
                }
                
                Shockers[shockerName].isGrabbed = isGrabbed;
                return;
            
            case "Cooldown":
                Shockers[shockerName].hasCooldownParam = true;
                return;
            
            case "":
                break;

            default:
                return;
        }

        if (value is OscTrue)
        {
            Shockers[shockerName].triggerMethod = TriggerMethod.Manual;
            Shockers[shockerName].lastActive = DateTime.UtcNow;
        }
        else Shockers[shockerName].triggerMethod = TriggerMethod.None;
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
            if (!shocker.hasCooldownParam)
                continue;
            
            var onCoolDown = shocker.lastExecuted > DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(Config.ConfigInstance.Behaviour.CooldownTime));
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
            if (shocker.triggerMethod == TriggerMethod.None || shocker.lastActive.AddMilliseconds(Config.ConfigInstance.Behaviour.HoldTime) > DateTime.UtcNow)
                return;
            
            if (shocker.lastExecuted >
                DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(Config.ConfigInstance.Behaviour.CooldownTime)))
            {
                shocker.triggerMethod = TriggerMethod.None;
                return;
            }
            shocker.lastExecuted = DateTime.UtcNow;

            byte intensity;
            uint duration;

            var beh = Config.ConfigInstance.Behaviour;
            if (beh.RandomDuration)
            {
                var rdr = beh.RandomDurationRange;
                duration = (uint)(Random.Next((int)(rdr.Min / beh.RandomDurationStep), (int)(rdr.Max / beh.RandomDurationStep)) * beh.RandomDurationStep);
            }
            else duration = beh.FixedDuration;

            if (beh.RandomIntensity)
            {
                var rir = beh.RandomIntensityRange;
                intensity = (byte)Random.Next((int)rir.Min, (int)rir.Max);
            }
            else intensity = beh.FixedIntensity;

            if (shocker.triggerMethod == TriggerMethod.PhysBoneRelease)
            {
                var rir = beh.RandomIntensityRange;
                intensity = (byte)LerpFloat(rir.Min, rir.Max, shocker.lastStretchValue);
                shocker.lastStretchValue = 0;
            }

            shocker.triggerMethod = TriggerMethod.None;
            var inSeconds = (float)duration / 1000;
            Log.Information("Sending shock to {Shocker} with {Intensity}:{Duration}", pos,
                intensity, inSeconds);

            var code = Config.ConfigInstance.ShockLink.Shockers[pos];
            await UserHubClient.Control(new Control
            {
                Id = code,
                Intensity = intensity,
                Duration = duration,
                Type = ControlType.Shock
            });

            if (!Config.ConfigInstance.Osc.Chatbox) return;
            var msg = $"Shock on {pos} with {intensity}:{inSeconds.ToString(CultureInfo.InvariantCulture)}";
            await SenderClient.SendMessageAsync(Config.ConfigInstance.Osc.Hoscy
                ? new OscMessage(new Address("/hoscy/message"), new[] { msg })
                : new OscMessage(new Address("/chatbox/input"), new object[] { msg, OscTrue.True }));
        }
    }

    private static float LerpFloat(float min, float max, float t)
    {
        return min + (max - min) * t;
    }
}