using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Serilog;

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

    public static Task InitializeAsync() => Connection.StartAsync();
    
    public static Task Control(params Control[] data)
    {
        return Connection.SendAsync("Control", data);
    }
}