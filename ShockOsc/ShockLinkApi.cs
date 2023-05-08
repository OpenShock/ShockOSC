using System.Net.WebSockets;
using System.Threading.Channels;
using Serilog;

namespace ShockOsc;

public static class ShockLinkApi
{
    private static ClientWebSocket _webSocket = null!;
    private static CancellationTokenSource _close = null!;
    private static readonly Channel<BaseRequest> Channel = System.Threading.Channels.Channel.CreateUnbounded<BaseRequest>();

    public static readonly ILogger Logger = Log.Logger.ForContext(typeof(ShockLinkApi));
    public static ValueTask QueueMessage(BaseRequest data) => Channel.Writer.WriteAsync(data);
    
    public static async Task Control(Control data)
    {
        await QueueMessage(new BaseRequest
        {
            RequestType = RequestType.Control,
            Data = new List<Control>
            {
                data
            }
        });
    }

    public static async Task MessageLoop()
    {
        try
        {
            await foreach (var msg in Channel.Reader.ReadAllAsync(_close.Token))
                await WebSocketUtils.SendFullMessage(
                    msg, _webSocket, _close.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error in message loop");
        }
    }

    public static Task Initialize()
    {
        return ConnectAsync();
    }

    private static async Task ConnectAsync()
    {
        if (_close != null) _close.Cancel();
        _close = new CancellationTokenSource();

        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("ShockLinkToken",
            Config.ConfigInstance.ShockLink.ApiToken);
        _webSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        Logger.Information("Connecting to websocket");
        try
        {
            await _webSocket.ConnectAsync(new Uri("wss://api.shocklink.net/1/ws/user"), _close.Token);
            Logger.Information("Connected to websocket");

            SlTask.Run(ReceiveLoop, _close.Token);
            SlTask.Run(MessageLoop, _close.Token);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error while connecting, retrying in 3 seconds");
            _webSocket.Abort();
            _webSocket.Dispose();
            await Task.Delay(3000);
            SlTask.Run(ConnectAsync);
        }
    }

    public static async Task ReceiveLoop()
    {
        WebSocketReceiveResult result = null;
        do
        {
            try
            {
                if (_webSocket.State == WebSocketState.Aborted) break;
                var message = await WebSocketUtils.ReceiveFullMessage(_webSocket, _close.Token);
                result = message.Item1;
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
                Logger.Warning("Lost websocket connection, trying to reconnect in 3 seconds");
                _webSocket.Abort();
                _webSocket.Dispose();

                await Task.Delay(3000);

                SlTask.Run(() => ConnectAsync());
                return;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error in receive loop");
            }
        } while (result is { CloseStatus: null });

        // Fallback, should not be reached unless api wants shutdown

        Logger.Warning("Lost websocket connection, trying to reconnect in 3 seconds");
        _webSocket.Abort();
        _webSocket.Dispose();

        await Task.Delay(3000);

        SlTask.Run(() => ConnectAsync());
    }
}