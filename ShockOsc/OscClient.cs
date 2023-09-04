using System.Net;
using System.Threading.Channels;
using LucHeart.CoreOSC;
using Serilog;

namespace ShockLink.ShockOsc;

public static class OscClient
{
    private static readonly OscDuplex GameConnection = new(new IPEndPoint(IPAddress.Loopback, Config.ConfigInstance.Osc.ReceivePort), new IPEndPoint(IPAddress.Loopback, Config.ConfigInstance.Osc.SendPort));
    private static readonly OscSender HoscySenderClient = new(new IPEndPoint(IPAddress.Loopback, Config.ConfigInstance.Osc.HoscySendPort));
    private static readonly ILogger Logger = Log.ForContext(typeof(OscClient));

    static OscClient()
    {
        Task.Run(GameSenderLoop);
        Task.Run(HoscySenderLoop);
    }

    private static readonly Channel<OscMessage> GameSenderChannel = Channel.CreateUnbounded<OscMessage>(new UnboundedChannelOptions()
    {
        SingleReader = true
    });
    
    private static readonly Channel<OscMessage> HoscySenderChannel = Channel.CreateUnbounded<OscMessage>(new UnboundedChannelOptions()
    {
        SingleReader = true
    });
    
    public static ValueTask SendGameMessage(string address, params object?[]?arguments)
    {
        arguments ??= Array.Empty<object>();
        return GameSenderChannel.Writer.WriteAsync(new OscMessage(address, arguments));
    }
    
    public static ValueTask SendChatboxMessage(string message)
    {
        if (Config.ConfigInstance.Osc.Hoscy) return HoscySenderChannel.Writer.WriteAsync(new OscMessage("/hoscy/message", message));

        return GameSenderChannel.Writer.WriteAsync(new OscMessage("/chatbox/input", message, true));
    }

    private static async Task GameSenderLoop()
    {
        Logger.Debug("Starting game sender loop");
        await foreach (var oscMessage in GameSenderChannel.Reader.ReadAllAsync())
        {
            try
            {
                await GameConnection.SendAsync(oscMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "GameSenderClient send failed");
            }
        }
    }

    private static async Task HoscySenderLoop()
    {
        Logger.Debug("Starting hoscy sender loop");
        await foreach (var oscMessage in HoscySenderChannel.Reader.ReadAllAsync())
        {
            try
            {
                await HoscySenderClient.SendAsync(oscMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "HoscySenderClient send failed");
            }
        }
    }

    public static Task<OscMessage> ReceiveGameMessage() => GameConnection.ReceiveMessageAsync();
}