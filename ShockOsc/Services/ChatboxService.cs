using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.ShockOSC.Config;
using OpenShock.ShockOSC.Utils;
using SmartFormat;

namespace OpenShock.ShockOSC.Services;

/// <summary>
/// Handle chatbox interactions and behaviour
/// </summary>
public sealed class ChatboxService : IAsyncDisposable
{
    private readonly IModuleConfig<ShockOscConfig> _moduleConfig;
    private readonly OscClient _oscClient;
    private readonly ILogger<ChatboxService> _logger;
    private readonly System.Threading.Timer _clearTimer;
    
    private readonly CancellationTokenSource _cts = new();

    private readonly Channel<Message> _messageChannel = Channel.CreateBounded<Message>(new BoundedChannelOptions(4)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public ChatboxService(IModuleConfig<ShockOscConfig> moduleConfig, OscClient oscClient, ILogger<ChatboxService> logger)
    {
        _moduleConfig = moduleConfig;
        _oscClient = oscClient;
        _logger = logger;

        _clearTimer = new System.Threading.Timer(ClearChatbox);

        OsTask.Run(MessageLoop);
    }

    private async void ClearChatbox(object? state)
    {
        try
        {
            await _oscClient.SendChatboxMessage(string.Empty);
            _logger.LogTrace("Cleared chatbox");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send clear chatbox");
        }
    }

    public async ValueTask SendLocalControlMessage(string name, byte intensity, uint duration, ControlType type)
    {
        if (!_moduleConfig.Config.Chatbox.Enabled) return;

        if (!_moduleConfig.Config.Chatbox.Types.TryGetValue(type, out var template))
        {
            _logger.LogError("No message template found for control type {ControlType}", type);
            return;
        }

        if (!template.Enabled) return;

        // Chatbox message local
        var dat = new
        {
            GroupName = name,
            ShockerName = name,
            Intensity = intensity,
            Duration = duration,
            DurationSeconds = duration.DurationInSecondsString()
        };

        var msg = $"{_moduleConfig.Config.Chatbox.Prefix}{Smart.Format(template.Local, dat)}";

        await _messageChannel.Writer.WriteAsync(new Message(msg, _moduleConfig.Config.Chatbox.TimeoutTimeSpan));
    }

    public async ValueTask SendRemoteControlMessage(string shockerName, string senderName, string? customName,
        byte intensity, uint duration, ControlType type)
    {
        if (!_moduleConfig.Config.Chatbox.Enabled || !_moduleConfig.Config.Chatbox.DisplayRemoteControl) return;

        if (!_moduleConfig.Config.Chatbox.Types.TryGetValue(type, out var template))
        {
            _logger.LogError("No message template found for control type {ControlType}", type);
            return;
        }

        if (!template.Enabled) return;

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

        var msg = $"{_moduleConfig.Config.Chatbox.Prefix}{Smart.Format(templateToUse, dat)}";

        await _messageChannel.Writer.WriteAsync(new Message(msg, _moduleConfig.Config.Chatbox.TimeoutTimeSpan));
    }
    
    public async ValueTask SendGenericMessage(string message)
    {
        if (!_moduleConfig.Config.Chatbox.Enabled) return;

        var msg = $"{_moduleConfig.Config.Chatbox.Prefix}{message}";
        await _messageChannel.Writer.WriteAsync(new Message(msg, _moduleConfig.Config.Chatbox.TimeoutTimeSpan));
    }

    private async Task MessageLoop()
    {
        await foreach (var message in _messageChannel.Reader.ReadAllAsync())
        {
            await _oscClient.SendChatboxMessage(message.Text);
            
            if(_moduleConfig.Config.Osc.Hoscy) continue;
            // We dont need to worry about timeouts if we're using hoscy
            if(_moduleConfig.Config.Chatbox.TimeoutEnabled) _clearTimer.Change(message.Timeout, Timeout.InfiniteTimeSpan);
            await Task.Delay(1250); // VRChat chatbox rate limit
        }
    }

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _clearTimer.DisposeAsync();
        
        await _cts.CancelAsync();
        _cts.Dispose();
        
        GC.SuppressFinalize(this);
    }
    
    ~ChatboxService()
    {
        if (_disposed) return;
        DisposeAsync().AsTask().Wait();
    }
}

public record Message(string Text, TimeSpan Timeout);