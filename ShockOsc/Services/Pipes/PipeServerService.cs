using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Utils;
using OpenShock.ShockOsc.Utils;

namespace OpenShock.ShockOsc.Services.Pipes;

public sealed class PipeServerService
{
    private readonly ILogger<PipeServerService> _logger;
    private uint _clientCount = 0;

    public PipeServerService(ILogger<PipeServerService> logger)
    {
        _logger = logger;
    }

    public ConcurrentQueue<PipeMessage> MessageQueue { get; } = new();
    public event Func<Task>? OnMessageReceived;

    public void StartServer()
    {
        OsTask.Run(ServerLoop);
    }

    private async Task ServerLoop()
    {
        var id = _clientCount++;

        await using var pipeServerStream = new NamedPipeServerStream("OpenShock.ShockOsc", PipeDirection.In, 20,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);


        _logger.LogInformation("[{Id}] Starting new server loop", id);

        await pipeServerStream.WaitForConnectionAsync();
#pragma warning disable CS4014
        OsTask.Run(ServerLoop);
#pragma warning restore CS4014

        _logger.LogInformation("[{Id}] Pipe connected!", id);

        using var reader = new StreamReader(pipeServerStream);
        while (pipeServerStream.IsConnected && !reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
            {
                _logger.LogWarning("[{Id}] Received empty pipe message. Skipping...", id);
                continue;
            }

            try
            {
                var jsonObj = JsonSerializer.Deserialize<PipeMessage>(line);
                if (jsonObj is null)
                {
                    _logger.LogWarning("[{Id}] Failed to deserialize pipe message. Skipping...", id);
                    continue;
                }

                MessageQueue.Enqueue(jsonObj);
                await OnMessageReceived.Raise();
                _logger.LogInformation("[{Id}], Received pipe message of type: {Type}", id, jsonObj.Type);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[{Id}] Failed to deserialize pipe message. Skipping...", id);
            }
        }

        _logger.LogInformation("[{Id}] Pipe disconnected. Stopping server loop...", id);
    }
}