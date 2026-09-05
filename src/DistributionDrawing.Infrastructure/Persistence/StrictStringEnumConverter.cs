using System.Text.Json;
using System.Text.Json.Serialization;

namespace DistributionDrawing.Infrastructure.Persistence;

internal sealed class StrictStringEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly JsonConverter<TEnum> Inner =
        (JsonConverter<TEnum>)new JsonStringEnumConverter<TEnum>(
            namingPolicy: null,
            allowIntegerValues: false).CreateConverter(
                typeof(TEnum),
                new JsonSerializerOptions());

    public override TEnum Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return Inner.Read(ref reader, typeToConvert, options);
    }

    public override void Write(
        Utf8JsonWriter writer,
        TEnum value,
        JsonSerializerOptions options)
    {
        Inner.Write(writer, value, options);
    }
}
