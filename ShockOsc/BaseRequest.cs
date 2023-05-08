using System.Text.Json;

namespace ShockOsc;

public class BaseRequest
{
    public required RequestType RequestType { get; set; }
    public object? Data { get; set; }
}