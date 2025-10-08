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
    Converters =
    [
        typeof(JsonStringEnumConverter<OldSchema.ChatboxConf.HoscyMessageType>),
        typeof(JsonStringEnumConverter<OldSchema.ControlType>),
        typeof(JsonStringEnumConverter<OldSchema.UpdateChannel>),
        typeof(JsonStringEnumConverter<OldSchema.BoneAction>),
        typeof(SemVersionJsonConverter)
    ])]
internal partial class OldSchemaSourceGenerationContext : JsonSerializerContext;

[JsonSerializable(typeof(NewSchema.ShockOscConfig))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    WriteIndented = true,
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    Converters =
    [
        typeof(JsonStringEnumConverter<NewSchema.ChatboxConf.HoscyMessageType>),
        typeof(JsonStringEnumConverter<NewSchema.ControlType>),
        typeof(JsonStringEnumConverter<NewSchema.BoneAction>),
        typeof(SemVersionJsonConverter)
    ])]
internal partial class NewSchemaSourceGenerationContext : JsonSerializerContext;