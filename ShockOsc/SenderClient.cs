using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using CoreOSC;
using CoreOSC.IO;
using Serilog;

namespace ShockLink.ShockOsc;

public static class SenderClient
{
    private static readonly UdpClient GameSenderClient = new();
    private static readonly UdpClient HoscySenderClient = new();
    private static readonly ILogger Logger = Log.ForContext(typeof(SenderClient));

    static SenderClient()
    {
        GameSenderClient.Connect(IPAddress.Loopback, (int)Config.ConfigInstance.Osc.SendPort);
        if(Config.ConfigInstance.Osc.Hoscy) HoscySenderClient.Connect(IPAddress.Loopback, (int)Config.ConfigInstance.Osc.HoscySendPort);
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


    public static ValueTask SendOscMessage(string address, IEnumerable<object>? arguments = null)
    {
        arguments ??= Array.Empty<object>();
        return GameSenderChannel.Writer.WriteAsync(new OscMessage(new Address(address), arguments));
    }
    
    public static ValueTask SendChatboxMessage(string message)
    {
        if (Config.ConfigInstance.Osc.Hoscy)
        {
            return HoscySenderChannel.Writer.WriteAsync(
                new OscMessage(new Address("/hoscy/message"), new[] { message }));
        }

        return GameSenderChannel.Writer.WriteAsync(new OscMessage(new Address("/chatbox/input"),
            new object[] { message, OscTrue.True }));
    }


    public static async Task GameSenderLoop()
    {
        Logger.Debug("Starting game sender loop");
        await foreach (var oscMessage in GameSenderChannel.Reader.ReadAllAsync())
        {
            try
            {
                await GameSenderClient.SendMessageAsync(oscMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "GameSenderClient send failed");
            }
        }
    }
    
    public static async Task HoscySenderLoop()
    {
        Logger.Debug("Starting hoscy sender loop");
        await foreach (var oscMessage in HoscySenderChannel.Reader.ReadAllAsync())
        {
            try
            {
                await HoscySenderClient.SendMessageAsync(oscMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "HoscySenderClient send failed");
            }
        }
    }
    
}