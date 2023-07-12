using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Serilog;
using ShockLink.ShockOsc.Models;
using ILogger = Serilog.ILogger;

namespace ShockLink.ShockOsc;

public static class UserHubClient
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UserHubClient));
    public static string? ConnectionId { get; set; }
    
    private static readonly HubConnection Connection = new HubConnectionBuilder()
        .WithUrl(Config.ConfigInstance.ShockLink.UserHub, HttpTransportType.WebSockets,
            options => { options.Headers.Add("ShockLinkToken", Config.ConfigInstance.ShockLink.ApiToken); })
        .WithAutomaticReconnect()
        .ConfigureLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddSerilog();
        })
        .Build();

    static UserHubClient()
    {
        Connection.On<ControlLogSender, ICollection<ControlLog>>("Log", LogReceive);
        Connection.On<string>("Welcome", WelcomeReceive);
    }

    public static Task InitializeAsync() => Connection.StartAsync();
    
    public static Task Control(params Control[] data) => Connection.SendAsync("ControlV2", data, "ShockOsc");

    #region Handlers

    private static Task WelcomeReceive(string connectionId)
    {
        ConnectionId = connectionId;
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