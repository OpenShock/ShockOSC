namespace ShockLink.ShockOsc.Models;

public class ControlLog
{
    public required GenericIn Shocker { get; set; }
    public required ControlType Type { get; set; }
    public required byte Intensity { get; set; }
    public required uint Duration { get; set; }
}