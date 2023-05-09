using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using CoreOSC;
using CoreOSC.IO;
using Serilog;

namespace ShockLink.ShockOsc;

public static class Program
{
    private static readonly ConcurrentDictionary<string, DateTime> Cooldown = new();
    private static readonly ConcurrentDictionary<string, DateTime> Active = new();
    private static readonly Random Random = new();

    private static readonly UdpClient ReceiverClient = new((int)Config.ConfigInstance.Osc.ReceivePort);

    private static readonly UdpClient SenderClient =
        new(new IPEndPoint(IPAddress.Loopback, 0));

    private static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
        
        Log.Information("Starting ShockLink.ShockOsc version {Version}", Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "error");
        Log.Information("Found shockers: {Shockers}", Config.ConfigInstance.ShockLink.Shockers.Select(x => x.Key));

        Log.Information("Connecting UDP Clients...");
        SenderClient.Connect(IPAddress.Loopback, (int)Config.ConfigInstance.Osc.SendPort);

        await ShockLinkApi.Initialize();

        // Start tasks
#pragma warning disable CS4014
        SlTask.Run(ReceiverLoopAsync);
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
        /*
            if(!addr.StartsWith("/avatar/parameters/ShockOsc")) continue;
            
            var pos = addr.Substring(28, addr.Length - 28);
            if (!Config.ConfigInstance.ShockLink.Shockers.ContainsKey(pos))
            {
                Log.Warning("Unknown shocker {Shocker}", pos);
                continue;
            }*/

        if (addr != "/avatar/parameters/Nsfw/BD/ToggleToy") return;
        var pos = "Leg/Left";

        var value = received.Arguments.ElementAtOrDefault(0);

        if (value is OscTrue) Active[pos] = DateTime.UtcNow;
        else Active.TryRemove(pos, out _);
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
        foreach (var shocker in Active.Where(shocker =>
                     shocker.Value.AddMilliseconds(Config.ConfigInstance.Behaviour.HoldTime) <= DateTime.UtcNow))
        {
            var pos = shocker.Key;
            Active.Remove(pos, out _);

            if (Cooldown.TryGetValue(pos, out var lastExecution) && lastExecution >
                DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(Config.ConfigInstance.Behaviour.CooldownTime)))
                return;
            Cooldown[pos] = DateTime.UtcNow;

            byte intensity;
            uint duration;

            if (Config.ConfigInstance.Behaviour.RandomDuration)
            {
                var rdr = Config.ConfigInstance.Behaviour.RandomDurationRange;
                duration = (uint)Random.Next((int)rdr.Min, (int)rdr.Max);
            }
            else duration = Config.ConfigInstance.Behaviour.FixedDuration;

            if (Config.ConfigInstance.Behaviour.RandomIntensity)
            {
                var rir = Config.ConfigInstance.Behaviour.RandomIntensityRange;
                intensity = (byte)Random.Next((int)rir.Min, (int)rir.Max);
            }
            else intensity = Config.ConfigInstance.Behaviour.FixedIntensity;

            Log.Information("Sending shock to {Shocker} with {Intensity}:{Duration}", pos,
                intensity, duration);

            var code = Config.ConfigInstance.ShockLink.Shockers[pos];
            await ShockLinkApi.Control(new Control
            {
                Id = code,
                Intensity = intensity,
                Duration = duration * 1000,
                Type = ControlType.Shock
            });

            if (!Config.ConfigInstance.Osc.Chatbox) return;
            var msg = $"Shock on {pos} with {intensity}:{duration}";
            await SenderClient.SendMessageAsync(Config.ConfigInstance.Osc.Hoscy
                ? new OscMessage(new Address("/hoscy/message"), new[] { msg })
                : new OscMessage(new Address("/chatbox/input"), new object[] { msg, OscTrue.True }));
        }
    }
}