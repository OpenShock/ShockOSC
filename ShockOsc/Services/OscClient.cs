﻿using System.Net;
using System.Threading.Channels;
using LucHeart.CoreOSC;
using Microsoft.Extensions.Logging;
using OpenShock.ShockOsc.Config;

namespace OpenShock.ShockOsc.Services;

public sealed class OscClient
{
    private readonly ILogger<OscClient> _logger;
    private readonly ConfigManager _configManager;
    private OscDuplex? _gameConnection;
    private readonly OscSender _hoscySenderClient;

    public OscClient(ILogger<OscClient> logger, ConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
        _hoscySenderClient = new OscSender(new IPEndPoint(IPAddress.Loopback, _configManager.Config.Osc.HoscySendPort));
        
        Task.Run(GameSenderLoop);
        Task.Run(HoscySenderLoop);
    }

    public void CreateGameConnection(IPAddress ipAddress, ushort receivePort, ushort sendPort)
    {
        _gameConnection?.Dispose();
        _gameConnection = null;
        _logger.LogDebug("Creating game connection with receive port {ReceivePort} and send port {SendPort}", receivePort, sendPort);
        _gameConnection = new(new IPEndPoint(ipAddress, receivePort), new IPEndPoint(ipAddress, sendPort));
    }

    private readonly Channel<OscMessage> _gameSenderChannel = Channel.CreateUnbounded<OscMessage>(new UnboundedChannelOptions
    {
        SingleReader = true
    });
    
    private readonly Channel<OscMessage> _hoscySenderChannel = Channel.CreateUnbounded<OscMessage>(new UnboundedChannelOptions
    {
        SingleReader = true
    });
    
    public ValueTask SendGameMessage(string address, params object?[]?arguments)
    {
        arguments ??= [];
        return _gameSenderChannel.Writer.WriteAsync(new OscMessage(address, arguments));
    }
    
    public ValueTask SendChatboxMessage(string message)
    {
        if (_configManager.Config.Osc.Hoscy) return _hoscySenderChannel.Writer.WriteAsync(new OscMessage(
            $"/hoscy/{_configManager.Config.Chatbox.HoscyType.ToString().ToLowerInvariant()}", message));
        return _gameSenderChannel.Writer.WriteAsync(new OscMessage("/chatbox/input", message, true));
    }

    private async Task GameSenderLoop()
    {
        _logger.LogDebug("Starting game sender loop");
        await foreach (var oscMessage in _gameSenderChannel.Reader.ReadAllAsync())
        {
            if (_gameConnection == null) continue;
            try
            {
                await _gameConnection.SendAsync(oscMessage);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GameSenderClient send failed");
            }
        }
    }

    private async Task HoscySenderLoop()
    {
        _logger.LogDebug("Starting hoscy sender loop");
        await foreach (var oscMessage in _hoscySenderChannel.Reader.ReadAllAsync())
        {
            try
            {
                await _hoscySenderClient.SendAsync(oscMessage);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "HoscySenderClient send failed");
            }
        }
    }

    public Task<OscMessage>? ReceiveGameMessage()
    {
        return _gameConnection?.ReceiveMessageAsync();
    }
}