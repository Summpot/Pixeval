// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Misaki;
using Pixeval.AppManagement;

namespace Pixeval.Models.Pixiv;

public record NovelContent
{
    public static NovelContent CreateDefault() => new();

    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("seriesId")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long? SeriesId { get; set; }

    [JsonPropertyName("seriesTitle")]
    public string? SeriesTitle { get; set; } = "";

    [JsonPropertyName("seriesIsWatched")]
    public bool? SeriesIsWatched { get; set; }

    [JsonPropertyName("userId")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long UserId { get; set; }

    [JsonPropertyName("coverUrl")]
    public string CoverUrl { get; set; } = "";

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; set; } = [];

    [JsonPropertyName("caption")]
    public string Caption { get; set; } = "";

    [JsonPropertyName("cdate")]
    public DateTimeOffset Date { get; set; }

    [JsonPropertyName("rating")]
    public Rating Rating { get; set; } = new();

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("marker")]
    public NovelMarker? Marker { get; set; }

    [JsonPropertyName("illusts")]
    [JsonConverter(typeof(NovelIllustrationInfoDictionaryConverter))]
    public IReadOnlyList<NovelIllustration> Illustrations { get; set; } = [];

    [JsonPropertyName("images")]
    [JsonConverter(typeof(NovelImageDictionaryConverter))]
    public IReadOnlyList<NovelImage> Images { get; set; } = [];

    [JsonPropertyName("seriesNavigation")]
    public SeriesNavigation? SeriesNavigation { get; set; }

    [JsonPropertyName("glossaryItems")]
    public IReadOnlyList<NovelReplaceableGlossary> GlossaryItems { get; set; } = [];

    [JsonPropertyName("replaceableItemIds")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public IReadOnlyList<long> ReplaceableItemIds { get; set; } = [];

    [JsonPropertyName("aiType")]
    public AiType AiType { get; set; }

    [JsonPropertyName("isOriginal")]
    public bool IsOriginal { get; set; }

    [JsonPropertyName("seasonalEffectTagData")]
    public string? SeasonalEffectTagData { get; set; } = "";

    [JsonPropertyName("eventBanners")]
    public string? EventBanners { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";
}

public record Rating
{
    [JsonPropertyName("like")]
    public int Like { get; set; }

    [JsonPropertyName("bookmark")]
    public int Bookmark { get; set; }

    [JsonPropertyName("view")]
    public int View { get; set; }
}

public record SeriesNavigation
{
    [JsonPropertyName("nextNovel")]
    public NovelNavigation? NextNovel { get; set; }

    [JsonPropertyName("prevNovel")]
    public NovelNavigation? PrevNovel { get; set; }
}

public record NovelNavigation
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }

    [JsonPropertyName("viewable")]
    public bool Viewable { get; set; }

    [JsonPropertyName("contentOrder")]
    public string ContentOrder { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("coverUrl")]
    public string CoverUrl { get; set; } = AppInfo.ImageNotAvailablePath;

    [JsonPropertyName("viewableMessage")]
    public string? ViewableMessage { get; set; }
}

public record NovelImage
{
    [JsonPropertyName("novelImageId")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long NovelImageId { get; set; }

    [JsonPropertyName("sl")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int Sl { get; set; }

    [JsonPropertyName("urls")]
    public NovelImageUrls Urls { get; set; } = new();

    public string ThumbnailUrl => Urls.X1200;

    public string OriginalUrl => Urls.Original;
}

public record NovelImageUrls
{
    [JsonPropertyName("240mw")]
    public string Mw240 { get; set; } = AppInfo.ImageNotAvailablePath;

    [JsonPropertyName("480mw")]
    public string Mw480 { get; set; } = AppInfo.ImageNotAvailablePath;

    [JsonPropertyName("1200x1200")]
    public string X1200 { get; set; } = AppInfo.ImageNotAvailablePath;

    [JsonPropertyName("128x128")]
    public string X128 { get; set; } = AppInfo.ImageNotAvailablePath;

    [JsonPropertyName("original")]
    public string Original { get; set; } = AppInfo.ImageNotAvailablePath;
}

public record NovelIllustration : IIdEntry
{
    [JsonPropertyName("visible")]
    public bool Visible { get; set; }

    [JsonPropertyName("availableMessage")]
    public string? AvailableMessage { get; set; }

    [JsonPropertyName("illust")]
    public NovelIllustrationInfo Illustration { get; set; } = new();

    [JsonPropertyName("user")]
    public NovelUser User { get; set; } = new();

    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    public string ThumbnailUrl => Illustration.Images.Medium;

    public Uri WebsiteUri => new($"https://www.pixiv.net/artworks/{Id}");

    public Uri AppUri => new($"pixeval://illust/{Id}");
}

public record NovelIllustrationInfo
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("restrict")]
    public int Restrict { get; set; }

    [JsonPropertyName("xRestrict")]
    public int XRestrict { get; set; }

    [JsonPropertyName("sl")]
    public int Sl { get; set; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<NovelTag> Tags { get; set; } = [];

    [JsonPropertyName("images")]
    public NovelIllustrationUrls Images { get; set; } = new();
}

public record NovelTag
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";
}

public record NovelIllustrationUrls
{
    [JsonPropertyName("small")]
    public string? Small { get; set; }

    [JsonPropertyName("medium")]
    public string Medium { get; set; } = AppInfo.ImageNotAvailablePath;

    [JsonPropertyName("original")]
    public string? Original { get; set; }
}

public record NovelUser
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("image")]
    public string Image { get; set; } = AppInfo.ImageNotAvailablePath;
}

public record NovelReplaceableGlossary
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("overview")]
    public string Overview { get; set; } = "";

    [JsonPropertyName("coverImage")]
    public NovelImage Cover { get; set; } = new();
}

public record NovelMarker
{
    [JsonPropertyName("page")]
    public int Page { get; set; }
}

internal class NovelIllustrationInfoDictionaryConverter : SpecialDictionaryConverter<NovelIllustration>
{
}

internal class NovelImageDictionaryConverter : SpecialDictionaryConverter<NovelImage>
{
}

internal class SpecialDictionaryConverter<T> : JsonConverter<IReadOnlyList<T>>
{
    public override IReadOnlyList<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return null;

        if (reader.TokenType is JsonTokenType.StartArray && reader.Read() && reader.TokenType is JsonTokenType.EndArray)
            return [];

        var list = new List<T>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    continue;
                case JsonTokenType.PropertyName when !reader.Read():
                    throw new JsonException();
                case JsonTokenType.PropertyName:
                {
                    var propertyValue = JsonSerializer.Deserialize<T>(ref reader, options)!;
                    list.Add(propertyValue);
                    break;
                }
                case JsonTokenType.EndObject:
                    return [.. list];
            }
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T>? value, JsonSerializerOptions options) => throw new NotSupportedException();
}
