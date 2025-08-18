using System.Text.Json.Serialization;
using OpenShock.ShockOSC.MigrationInstaller.Serializer;

namespace OpenShock.ShockOSC.MigrationInstaller.Schemas;

[JsonSerializable(typeof(OldSchema.ShockOscConfig))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default, 
    WriteIndented = true, 
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, 
    PropertyNameCaseInsensitive = true,
    Converters = [typeof(JsonStringEnumConverter), typeof(SemVersionJsonConverter)])]
internal partial class OldSchemaSourceGenerationContext : JsonSerializerContext;

[JsonSerializable(typeof(NewSchema.ShockOscConfig))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default, 
    WriteIndented = true, 
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, 
    PropertyNameCaseInsensitive = true,
    Converters = [typeof(JsonStringEnumConverter), typeof(SemVersionJsonConverter)])]
internal partial class NewSchemaSourceGenerationContext : JsonSerializerContext;