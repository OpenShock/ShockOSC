﻿using System.Numerics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.Components;
using Size = MudBlazor.Size;

namespace OpenShock.ShockOsc.Ui.Utils;

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

    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public TimeSpan DebounceTime { get; set; } = TimeSpan.FromMilliseconds(500);

    [Parameter] public EventCallback<T> SliderValueChanged { get; set; }
    
    [Parameter]
    public Action<T>? OnValueChanged { get; set; }

    private T _sliderValue = default!;
    
    [Parameter]
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

    [Parameter] public Action<T>? OnSaveAction { get; set; }
    
    [Parameter]
    public Size Size { get; set; } = Size.Small;
    
    [Parameter]
    public string? Style { get; set; }
    
    [Parameter]
    public string? Class { get; set; }
    
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter] public T Min { get; set; } = T.Zero;
    
    [Parameter]
    public T Max { get; set; } = T.CreateTruncating(100);
    
    [Parameter]
    public T Step { get; set; } = T.One;

    private bool _disposed;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _subject?.Dispose();
    }
}