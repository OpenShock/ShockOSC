namespace ShockLink.ShockOsc;

public class BaseRequest
{
    public required RequestType RequestType { get; set; }
    public object? Data { get; set; }
}