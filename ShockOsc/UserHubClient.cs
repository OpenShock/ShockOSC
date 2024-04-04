using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Live;
using OpenShock.SDK.CSharp.Live.Models;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OpenShock.ShockOsc;

public static class UserHubClient
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UserHubClient));
    public static string? ConnectionId { get; set; }
    
    private static OpenShockApiLiveClient? _liveClient;


    public static async Task SetupLiveClient()
    {
        if(_liveClient != null) await _liveClient.DisposeAsync();
        _liveClient = new OpenShockApiLiveClient(new ApiLiveClientOptions()
        {
            Server = Config.ConfigInstance.OpenShock.Backend,
            Token = Config.ConfigInstance.OpenShock.Token,
            ConfigureLogging = builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddSerilog();
            }
        });

        _liveClient.OnLog(LogReceive);
        _liveClient.OnWelcome(WelcomeReceive);
        
        
       _liveClient.Connection.Closed += exception => {
            ShockOsc.SetAuthSate(ShockOsc.AuthState.NotAuthenticated);
            return Task.CompletedTask;
        };
       
        await _liveClient.StartAsync();
    }
    
    
    public static Task? Control(params Control[] data) => _liveClient?.Control(data, "ShockOsc");

    public static async Task Disconnect()
    {
        if (_liveClient != null) await _liveClient.DisposeAsync();
    }

    #region Handlers

    private static Task WelcomeReceive(string connectionId)
    {
        ConnectionId = connectionId;
        ShockOsc.SetAuthSate(ShockOsc.AuthState.Authenticated);
        return Task.CompletedTask;
    }
    
    private static async Task LogReceive(ControlLogSender sender, ICollection<ControlLog> logs)
    {
        Logger.Debug("Received Sender: {@Sender} Logs: {@Logs}", sender, logs);
        
        foreach (var log in logs)
            await ShockOsc.RemoteActivateShocker(sender, log);
    }

    #endregion
    
}