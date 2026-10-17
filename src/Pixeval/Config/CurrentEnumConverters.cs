using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixeval.Config;

public abstract class CurrentEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    protected virtual string Format(T value) => value.ToString();
    public T Parse(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? Parse(value.GetString()!) : throw new JsonException($"Invalid {typeof(T).Name} value.");

    public T Parse(string text)
    {
        foreach (var value in Enum.GetValues<T>()) if (Format(value) == text) return value;
        throw new JsonException($"Invalid {typeof(T).Name} value.");
    }
    public string ToText(T value) => Enum.IsDefined(value) ? Format(value) : throw new JsonException("Invalid enum value.");
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            foreach (var value in Enum.GetValues<T>())
                if (reader.ValueTextEquals(Format(value))) return value;
        throw new JsonException($"Invalid {typeof(T).Name} value.");
    }
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value)) throw new JsonException($"Invalid {typeof(T).Name} value.");
        writer.WriteStringValue(Format(value));
    }
}

public sealed class RankModeJsonConverter : CurrentEnumConverter<RankMode>
{
    protected override string Format(RankMode value) => value == RankMode.WeekR18G
        ? "week_r18g" : JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
}
public sealed class WorkKindJsonConverter : CurrentEnumConverter<WorkKind>
{
    protected override string Format(WorkKind value) => value == WorkKind.Illustration ? "illust" : value.ToString().ToLowerInvariant();
}
public sealed class SimpleWorkKindJsonConverter : CurrentEnumConverter<SimpleWorkKind>;
public sealed class PrivacyRestrictJsonConverter : CurrentEnumConverter<PrivacyRestrict>
{
    protected override string Format(PrivacyRestrict value) => value.ToString().ToLowerInvariant();
}

// STJ cannot inspect FFI enum declarations emitted by another source generator.
// Keep this boundary explicit without duplicating the domain enums.
public sealed class RankOptionsSettingsJsonConverter : JsonConverter<RankOptionsSettings>
{
    private static readonly RankModeJsonConverter Modes = new();
    public override RankOptionsSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var result = new RankOptionsSettings();
        foreach (var property in doc.RootElement.EnumerateObject())
            switch (property.Name)
            {
                case "IllustrationRankOption": result.IllustrationRankOption = Modes.Parse(property.Value); break;
                case "NovelRankOption": result.NovelRankOption = Modes.Parse(property.Value); break;
            }
        return result;
    }
    public override void Write(Utf8JsonWriter writer, RankOptionsSettings value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("IllustrationRankOption", Modes.ToText(value.IllustrationRankOption));
        writer.WriteString("NovelRankOption", Modes.ToText(value.NovelRankOption));
        writer.WriteEndObject();
    }
}
public sealed class SearchSettingsGroupJsonConverter : JsonConverter<SearchSettingsGroup>
{
    private static readonly SimpleWorkKindJsonConverter Kinds = new();
    private static readonly RankOptionsSettingsJsonConverter Ranks = new();
    public override SearchSettingsGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var result = new SearchSettingsGroup();
        foreach (var property in doc.RootElement.EnumerateObject())
            switch (property.Name)
            {
                case "RankOptions":
                    var rankReader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(property.Value.GetRawText()));
                    rankReader.Read();
                    result.RankOptions = Ranks.Read(ref rankReader, typeof(RankOptionsSettings), options); break;
                case "SauceNaoApiKey": result.SauceNaoApiKey = property.Value.GetString()!; break;
                case "DefaultSimpleWorkType": result.DefaultSimpleWorkType = Kinds.Parse(property.Value); break;
            }
        return result;
    }
    public override void Write(Utf8JsonWriter writer, SearchSettingsGroup value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("RankOptions"); Ranks.Write(writer, value.RankOptions, options);
        writer.WriteString("SauceNaoApiKey", value.SauceNaoApiKey);
        writer.WriteString("DefaultSimpleWorkType", Kinds.ToText(value.DefaultSimpleWorkType));
        writer.WriteEndObject();
    }
}
