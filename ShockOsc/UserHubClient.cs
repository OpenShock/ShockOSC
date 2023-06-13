using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Serilog;
using ShockLink.ShockOsc.Models;

namespace ShockLink.ShockOsc;

public static class UserHubClient
{
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
        Connection.On<GenericIni, IEnumerable<ControlLog>>("Log", LogReceive);
    }

    public static Task InitializeAsync() => Connection.StartAsync();
    
    public static Task Control(params Control[] data) => Connection.SendAsync("Control", data);

    #region Handlers

    private static Task LogReceive(GenericIni sender, IEnumerable<ControlLog> logs)
    {
        Log.Debug("Received: {Json}", JsonSerializer.Serialize(logs));
        
        foreach (var log in logs)
            ShockOsc.RemoteActivateShocker(log);
        
        return Task.CompletedTask;
    }

    #endregion
    
}