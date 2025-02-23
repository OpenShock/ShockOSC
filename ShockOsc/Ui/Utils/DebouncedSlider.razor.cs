using System.ComponentModel;
using System.Numerics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Size = MudBlazor.Size;

namespace OpenShock.ShockOSC.Ui.Utils;

public partial class DebouncedSlider<T> : ComponentBase, IDisposable where T : struct, INumber<T>
{
    
    private BehaviorSubject<T>? _subject;

    private T ValueProp
    {
        get => _subject!.Value;
        set
        {
            SliderValue = value;
            OnValueChanged?.Invoke(value);
        }
    }

    protected override void OnInitialized()
    {
        _subject = new BehaviorSubject<T>(SliderValue);
        _subject.Throttle(DebounceTime).Subscribe(value => OnSaveAction?.Invoke(value));
    }

    [Parameter] 
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public string Label { get; set; } = string.Empty;
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public TimeSpan DebounceTime { get; set; } = TimeSpan.FromMilliseconds(500);

    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EventCallback<T> SliderValueChanged { get; set; }
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<T>? OnValueChanged { get; set; }

    private T _sliderValue = default!;
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
#pragma warning disable BL0007
    public T SliderValue
#pragma warning restore BL0007
    {
        get => _sliderValue;
        set
        {
            _subject?.OnNext(value);
            if(_sliderValue.Equals(value)) return;
                
            SliderValueChanged.InvokeAsync(value);
            _sliderValue = value!;
        }
    }
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Action<T>? OnSaveAction { get; set; }
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Size Size { get; set; } = Size.Small;
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string? Style { get; set; }
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string? Class { get; set; }
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public T Min { get; set; } = T.Zero;
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public T Max { get; set; } = T.CreateTruncating(100);
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public T Step { get; set; } = T.One;

    private bool _disposed;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _subject?.Dispose();
    }
}