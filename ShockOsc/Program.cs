using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CoreOSC;
using CoreOSC.IO;
using Serilog;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ShockOsc;

public static class Program
{
    private static readonly ConcurrentDictionary<string, DateTime> Cooldown = new();
    private static readonly ConcurrentDictionary<string, Timer> ActiveTimers = new();
    private static readonly Random Random = new();

    private static readonly UdpClient ReceiverClient = new((int)Config.ConfigInstance.Osc.ReceivePort);

    private static readonly UdpClient SenderClient =
        new(new IPEndPoint(IPAddress.Loopback, (int)Config.ConfigInstance.Osc.SendPort));

    private static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        Log.Information("Found shockers: {Shockers}", Config.ConfigInstance.ShockLink.Shockers.Select(x => x.Key));

        await ShockLinkApi.Initialize();

        // Start the listen thread
        SlTask.Run(ReceiverLoopAsync);

        // wait for a key press to exit
        Log.Information("All started");
        await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
    }


    private static async Task ReceiverLoopAsync()
    {
        while (true)
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

            if (addr != "/avatar/parameters/Nsfw/BD/ToggleToy") continue;
            var pos = "Leg/Left";

            var value = received.Arguments.ElementAtOrDefault(0);
            
            if (value is OscTrue)
            {
                Console.WriteLine("New");
                var newTimer = new Timer(TimerElapsed,
                    pos, TimeSpan.FromMilliseconds(Config.ConfigInstance.Behaviour.HoldTime), Timeout.InfiniteTimeSpan);

                ActiveTimers[pos] = newTimer;
            }
            else if (ActiveTimers.TryRemove(pos, out var removed))
            {
                removed.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                await removed.DisposeAsync();
            }
        }
    }

    private static void TimerElapsed(object? state)
    {
        var pos = (string)state!;
        if (Cooldown.TryGetValue(pos, out var lastExecution) && lastExecution >
            DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(Config.ConfigInstance.Behaviour.CooldownTime))) return;
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
        SlTask.Run(() => ShockLinkApi.Control(new Control
        {
            Id = code,
            Intensity = intensity,
            Duration = duration * 1000,
            Type = ControlType.Shock
        }));

        if (!Config.ConfigInstance.Osc.Chatbox) return;
        var msg = $"Shock on {pos} with {intensity}:{duration}";
        /*SenderClient.SendMessageAsync(Config.ConfigInstance.Osc.Hoscy
            ? new OscMessage(new Address("/hoscy/message"), new[] { msg })
            : new OscMessage(new Address("/chatbox/input"), new[] { msg, (object)true }));*/
    }
}