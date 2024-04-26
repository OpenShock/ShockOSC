using Microsoft.AspNetCore.Components;
using OpenShock.SDK.CSharp.Live;
using OpenShock.SDK.CSharp.Live.LiveControlModels;
using Color = MudBlazor.Color;

namespace OpenShock.ShockOsc.Ui.Components.Parts;

public partial class StatePart : ComponentBase, IDisposable
{
    [Parameter]
    public required IOpenShockLiveControlClient Client { get; set; }
    
    [Parameter]
    public required string Text { get; set; }

    
    private Task StateOnValueChanged(WebsocketConnectionState state)
    {
        return InvokeAsync(StateHasChanged);
    }
    
    private Color GetConnectionStateColor() =>
        Client.State.Value switch
        {
            WebsocketConnectionState.Connected => Color.Success,
            WebsocketConnectionState.Reconnecting => Color.Warning,
            WebsocketConnectionState.Connecting => Color.Warning,
            WebsocketConnectionState.Disconnected => Color.Error,
            _ => Color.Error
        };
    
    protected override void OnInitialized()
    {
        Client.State.OnValueChanged += StateOnValueChanged;
        Client.Latency.OnValueChanged += LatencyOnValueChanged;
    }

    private Task LatencyOnValueChanged(ulong arg)
    {
        return InvokeAsync(StateHasChanged);
    }

    private bool _disposed = false;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Client.State.OnValueChanged -= StateOnValueChanged;
        Client.Latency.OnValueChanged -= LatencyOnValueChanged;
        
        GC.SuppressFinalize(this);
    }
}