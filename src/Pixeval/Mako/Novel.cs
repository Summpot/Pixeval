// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq;
using System.Text.Json.Serialization;
using Pixeval.Filters;
using Pixeval.Pixiv;
using Pixeval.Storage;

namespace Pixeval.Mako;

public partial record struct Novel : IWorkEntry, IArtworkSerializable
{
    [JsonIgnore]
    public bool IsBookmarkSupported => !BlockedContentHelper.IsBlockedPlaceholder(this) && Platform is PlatformConstants.Pixiv;

    [JsonIgnore]
    public bool HasSeries => Series is not null;

    [JsonIgnore]
    public double AspectRatio => 1;

    [JsonIgnore]
    public string? ThumbnailUrl => ImageUrls.SquareMedium ?? ImageUrls.Medium ?? ImageUrls.Large ?? "";

    [JsonIgnore]
    public string Tooltip => Title;

    [JsonIgnore]
    public string Platform => PlatformConstants.Pixiv;

    [JsonIgnore]
    public int Width => 0;

    [JsonIgnore]
    public int Height => 0;

    [JsonIgnore]
    public DateTimeOffset CreateDateOffset => DateTimeOffset.TryParse(CreateDate, out var dt) ? dt : default;

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixiv.net/novel/show.php?id={Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://novel/{Id}");

    [JsonIgnore]
    public SafeRating SafeRating => XRestrict switch
    {
        1 => SafeRating.Explicit,
        2 => SafeRating.Guro,
        _ => SafeRating.NotSpecified
    };

    [JsonIgnore]
    public XRestrict XRestrictValue => XRestrict switch
    {
        1 => Pixeval.Pixiv.XRestrict.R18,
        2 => Pixeval.Pixiv.XRestrict.R18G,
        _ => Pixeval.Pixiv.XRestrict.Ordinary
    };

    [JsonIgnore]
    public bool IsAiGenerated => NovelAiType == 2;

    public ArtworkMetadata ToArtworkMetadata() => new(
        Id.ToString(),
        Title ?? string.Empty,
        User.Name ?? string.Empty,
        User.Account ?? string.Empty,
        Tags.Select(t => new ArtworkTag(t.Name, t.TranslatedName ?? string.Empty)).ToList(),
        TotalBookmarks,
        CreateDateOffset.ToUnixTimeSeconds(),
        0,
        0,
        XRestrict,
        IsAiGenerated ? 1 : 0,
        0);

    public const string LegacyNovelToken = "Mako.Model.Novel";

    public string Serialize() => System.Text.Json.JsonSerializer.Serialize(this);

    [JsonIgnore]
    public string SerializeKey => LegacyNovelToken;

    private static readonly System.Text.Json.JsonSerializerOptions s_snakeCaseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly System.Text.Json.JsonSerializerOptions s_caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Novel Deserialize(string data)
    {
        try
        {
            var res = System.Text.Json.JsonSerializer.Deserialize<Novel>(data, s_snakeCaseOptions);
            if (res.Id != 0)
                return res;
        }
        catch
        {
            // fallback
        }

        try
        {
            var res = System.Text.Json.JsonSerializer.Deserialize<Novel>(data, s_caseInsensitiveOptions);
            if (res.Id != 0)
                return res;
        }
        catch
        {
            // fallback
        }

        return System.Text.Json.JsonSerializer.Deserialize<Novel>(data);

    }
}
