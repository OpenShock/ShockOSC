namespace OpenShock.ShockOsc.Ui.Utils;

public sealed class UpdateableVariable<T>(T internalValue)
{
    public T Value
    {
        get => internalValue;
        set
        {
            if (internalValue!.Equals(value)) return;
            internalValue = value;
            OnValueChanged?.Invoke(value);
        }
    }
    
    public event Action<T>? OnValueChanged;
    
    public void UpdateWithoutNotify(T newValue)
    {
        internalValue = newValue;
    }
}