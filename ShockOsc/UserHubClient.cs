using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using OpenShock.ShockOsc.Models;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OpenShock.ShockOsc;

public static class UserHubClient
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UserHubClient));
    public static string? ConnectionId { get; set; }

    private static HubConnection? Connection;

    public static async Task InitializeAsync()
    {
        Connection?.DisposeAsync();
        Connection = new HubConnectionBuilder()
            .WithUrl($"{Config.ConfigInstance.ShockLink.OpenShockApi}/hubs/user", HttpTransportType.WebSockets,
                options => { options.Headers.Add("OpenShockToken", Config.ConfigInstance.ShockLink.ApiToken); })
            .WithAutomaticReconnect()
            .ConfigureLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddSerilog();
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                options.PayloadSerializerOptions.Converters.Add(new CustomJsonStringEnumConverter());
            })
            .Build();
        Connection.On<ControlLogSender, ICollection<ControlLog>>("Log", LogReceive);
        Connection.On<string>("Welcome", WelcomeReceive);
        Connection.Closed += exception => {
            ShockOsc.SetAuthSate(ShockOsc.AuthState.NotAuthenticated);
            return Task.CompletedTask;
        };
        await Connection.StartAsync();
    }
    
    public static Task? Control(params Control[] data) => Connection?.SendAsync("ControlV2", data, "ShockOsc");

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