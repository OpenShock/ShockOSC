using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ShockOsc;

public static class WebSocketUtils
{
    public static async Task<(WebSocketReceiveResult, IEnumerable<byte>)> ReceiveFullMessage(
        WebSocket socket, CancellationToken cancelToken)
    {
        WebSocketReceiveResult response;
        var message = new List<byte>();

        var buffer = new byte[4096];
        do
        {
            response = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancelToken);
            message.AddRange(new ArraySegment<byte>(buffer, 0, response.Count));
        } while (response is { EndOfMessage: false, CloseStatus: null });

        return (response, message);
    }

    public static Task SendFullMessage<T>(T obj, WebSocket socket, CancellationToken cancelToken) =>
        SendFullMessage(JsonSerializer.Serialize(obj), socket, cancelToken);

    public static Task SendFullMessage(string json, WebSocket socket, CancellationToken cancelToken)  {
        Console.WriteLine(json);
        return SendFullMessageBytes(Encoding.UTF8.GetBytes(json), socket, cancelToken);
    }
        


    public static async Task SendFullMessageBytes(IEnumerable<byte> msg, WebSocket socket, CancellationToken cancelToken)
    {
        var buffer = Split(msg.ToArray(), 4096).ToArray();

        for (var i = 0; i < buffer.Length; i++)
        {
            var cur = buffer[i];
            await socket.SendAsync(new ArraySegment<byte>(cur), WebSocketMessageType.Text, i >= buffer.Length - 1,
                cancelToken);
        }
    }

    private static IEnumerable<byte[]> Split(IReadOnlyCollection<byte> value, int bufferLength)
    {
        var countOfArray = value.Count / bufferLength;
        if (value.Count % bufferLength > 0)
            countOfArray++;
        for (var i = 0; i < countOfArray; i++)
        {
            yield return value.Skip(i * bufferLength).Take(bufferLength).ToArray();
        }
    }
}