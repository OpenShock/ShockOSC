using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenShock.ShockOsc.Utils;

/// <summary>
/// JSON converter for <see cref="IPAddress"/> that uses the <see cref="IPAddress.TryFormat(Span{char}, out int)"/> method.
/// </summary>
public class JsonIPAddressConverter : JsonConverter<IPAddress>
{
    /// <inheritdoc/>
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException($"Expected string but got {reader.TokenType}.");
            
        Span<char> charData = stackalloc char[45];
        
        var count = Encoding.UTF8.GetChars(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan, charData);
        
        return !IPAddress.TryParse(charData[..count], out var value)
            ? throw new JsonException($"Could not parse IPAddress from [{charData[..count].ToString()}].")
            : value;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
    {
        var data = value.AddressFamily == AddressFamily.InterNetwork ? stackalloc char[15] : stackalloc char[45];
        if (!value.TryFormat(data, out var charsWritten)) throw new JsonException($"IPAddress [{value}] could not be written to JSON.");
        writer.WriteStringValue(data[..charsWritten]);
    }

}