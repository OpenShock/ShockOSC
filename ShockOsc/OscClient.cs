using System.Net;
using System.Threading.Channels;
using LucHeart.CoreOSC;
using Serilog;

namespace OpenShock.ShockOsc;

public static class OscClient
{
    private static OscDuplex? _gameConnection;
    private static readonly OscSender HoscySenderClient = new(new IPEndPoint(IPAddress.Loopback, ShockOscConfigManager.ConfigInstance.Osc.HoscySendPort));
    private static readonly ILogger Logger = Log.ForContext(typeof(OscClient));

    static OscClient()
    {
        Task.Run(GameSenderLoop);
        Task.Run(HoscySenderLoop);
    }

    public static void CreateGameConnection(IPAddress ipAddress, ushort receivePort, ushort sendPort)
    {
        _gameConnection?.Dispose();
        _gameConnection = null;
        Logger.Debug("Creating game connection with receive port {ReceivePort} and send port {SendPort}", receivePort, sendPort);
        _gameConnection = new(new IPEndPoint(ipAddress, receivePort), new IPEndPoint(ipAddress, sendPort));
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
        if (ShockOscConfigManager.ConfigInstance.Osc.Hoscy) return HoscySenderChannel.Writer.WriteAsync(new OscMessage("/hoscy/message", message));

        return GameSenderChannel.Writer.WriteAsync(new OscMessage("/chatbox/input", message, true));
    }

    private static async Task GameSenderLoop()
    {
        Logger.Debug("Starting game sender loop");
        await foreach (var oscMessage in GameSenderChannel.Reader.ReadAllAsync())
        {
            if (_gameConnection == null) continue;
            try
            {
                await _gameConnection.SendAsync(oscMessage);
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

    public static Task<OscMessage>? ReceiveGameMessage()
    {
        return _gameConnection?.ReceiveMessageAsync();
    }
}