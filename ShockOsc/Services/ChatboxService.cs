using System.Threading.Channels;
using OpenShock.SDK.CSharp.Models;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Utils;
using SmartFormat;

namespace OpenShock.ShockOsc.Services;

/// <summary>
/// Handle chatbox interactions and behaviour
/// </summary>
public sealed class ChatboxService
{
    private readonly ConfigManager _configManager;
    private readonly OscClient _oscClient;
    private readonly ILogger<ChatboxService> _logger;

    private Channel<Message> _messageChannel = Channel.CreateBounded<Message>(new BoundedChannelOptions(10)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public ChatboxService(ConfigManager configManager, OscClient oscClient, ILogger<ChatboxService> logger)
    {
        _configManager = configManager;
        _oscClient = oscClient;
        _logger = logger;
        
        OsTask.Run(MessageLoop);
    }

    public async ValueTask SendLocalControlMessage(string name, byte intensity, uint duration, ControlType type)
    {
        if (!_configManager.Config.Chatbox.Enabled) return;

        if (!_configManager.Config.Chatbox.Types.TryGetValue(type, out var template))
        {
            _logger.LogError("No message template found for control type {ControlType}", type);
            return;
        }
        
        if(!template.Enabled) return;
        
        // Chatbox message local
        var dat = new
        {
            GroupName = name,
            ShockerName = name,
            Intensity = intensity,
            Duration = duration,
            DurationSeconds = duration.DurationInSecondsString()
        };

        var msg = $"{_configManager.Config.Chatbox.Prefix}{Smart.Format(template.Local, dat)}";

        await _messageChannel.Writer.WriteAsync(new Message(msg, TimeSpan.FromSeconds(5)));
    }
    
    public async ValueTask SendRemoteControlMessage(string shockerName, string senderName, string? customName, byte intensity, uint duration, ControlType type)
    {
        if (!_configManager.Config.Chatbox.Enabled || !_configManager.Config.Chatbox.DisplayRemoteControl) return;

        if (!_configManager.Config.Chatbox.Types.TryGetValue(type, out var template))
        {
            _logger.LogError("No message template found for control type {ControlType}", type);
            return;
        }
        
        if(!template.Enabled) return;
        
        // Chatbox message remote
        var dat = new
        {
            ShockerName = shockerName,
            Intensity = intensity,
            Duration = duration,
            DurationSeconds = duration.DurationInSecondsString(),
            Name = senderName,
            CustomName = customName
        };
        
        var templateToUse = customName == null ? template.Remote : template.RemoteWithCustomName;

        var msg = $"{_configManager.Config.Chatbox.Prefix}{Smart.Format(templateToUse, dat)}";

        await _messageChannel.Writer.WriteAsync(new Message(msg, TimeSpan.FromSeconds(5)));
    }

    private async Task MessageLoop()
    {
        await foreach (var message in _messageChannel.Reader.ReadAllAsync())
        {
            await _oscClient.SendChatboxMessage(message.Text);
            await Task.Delay(message.Timeout);
        }
    }
}

public record Message(string Text, TimeSpan Timeout);