// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using Pixeval.Filters;
using Pixeval.Localization;
using Pixeval.Pixiv;
using Pixeval.Storage;

namespace Pixeval.Mako;

public partial record struct Illustration : IWorkEntry, IArtworkSerializable
{
    [JsonIgnore]
    public bool IsBookmarkSupported => !BlockedContentHelper.IsBlockedPlaceholder(this) && Platform is PlatformConstants.Pixiv;

    [JsonIgnore]
    public bool HasSeries => Series is not null;

    [JsonIgnore]
    public double AspectRatio => Width > 0 && Height > 0 ? (double) Width / Height : 1;

    [JsonIgnore]
    public string? SizeText => Width > 0 && Height > 0 ? $"{Width} x {Height}" : null;

    [JsonIgnore]
    public string? ThumbnailUrl => ImageUrls.Medium ?? ImageUrls.SquareMedium ?? ImageUrls.Large;

    [JsonIgnore]
    public string Tooltip
    {
        get
        {
            var sb = new StringBuilder(Title);
            if (IsPicGif)
                sb.AppendLine().Append(Strings.EntryItemTheIllustrationIsAnUgoira());
            else if (IsPicSet)
                sb.AppendLine().Append(Strings.EntryItemTheIllustrationIsAMangaFormatted(count: PageCount));
            return sb.ToString();
        }
    }

    [JsonIgnore]
    public string Platform => PlatformConstants.Pixiv;

    [JsonIgnore]
    public DateTimeOffset CreateDateOffset => DateTimeOffset.TryParse(CreateDate, out var dt) ? dt : default;

    [JsonIgnore]
    public IllustrationType Type => IllustType switch
    {
        "manga" => IllustrationType.Manga,
        "ugoira" => IllustrationType.Ugoira,
        _ => IllustrationType.Illustration
    };

    [JsonIgnore]
    public int SetIndex { get; init; } = -1;

    [JsonIgnore]
    public string? OriginalUrl => MetaSinglePage.OriginalImageUrl ?? ImageUrls.Original ?? ImageUrls.Large ?? ImageUrls.Medium;

    [JsonIgnore]
    public bool IsPicGif => string.Equals(IllustType, "ugoira", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsPicSet => PageCount > 1 || SetIndex > -1;

    [JsonIgnore]
    public bool IsPicOne => !IsPicSet && !IsPicGif;

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixiv.net/artworks/{Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://illust/{Id}");

    [JsonIgnore]
    public SafeRating SafeRating => XRestrict switch
    {
        1 => SafeRating.Explicit,
        2 => SafeRating.Guro,
        _ => RestrictionAttributes?.Any(a => string.Equals(a, "r18g", StringComparison.OrdinalIgnoreCase)) == true
            ? SafeRating.Guro
            : RestrictionAttributes?.Any(a => string.Equals(a, "r18", StringComparison.OrdinalIgnoreCase)) == true
                ? SafeRating.Explicit
                : RestrictionAttributes?.Any(a => string.Equals(a, "sensitive", StringComparison.OrdinalIgnoreCase)) == true
                    ? SafeRating.Questionable
                    : SanityLevel switch
                    {
                        6 => SafeRating.Questionable,
                        2 => SafeRating.General,
                        _ => SafeRating.NotSpecified
                    }
    };

    [JsonIgnore]
    public XRestrict XRestrictValue => XRestrict switch
    {
        1 => Pixeval.Pixiv.XRestrict.R18,
        2 => Pixeval.Pixiv.XRestrict.R18G,
        _ => Pixeval.Pixiv.XRestrict.Ordinary
    };

    [JsonIgnore]
    public bool IsAiGenerated => IllustAiType == 2;

    [JsonIgnore]
    public UgoiraMetadata? UgoiraMetadata { get; init; }

    [JsonIgnore]
    public IReadOnlyList<Illustration> Pages
    {
        get
        {
            if (PageCount <= 1) return [this];
            var self = this;
            return [.. MetaPages.Select((m, i) => self with
            {
                SetIndex = i,
                ImageUrls = m.ImageUrls,
                MetaSinglePage = new MetaSinglePage(m.ImageUrls.Original)
            })];
        }
    }

    public ArtworkMetadata ToArtworkMetadata() => new(
        Id.ToString(),
        Title ?? string.Empty,
        User.Name ?? string.Empty,
        User.Account ?? string.Empty,
        Tags.Select(t => new ArtworkTag(t.Name, t.TranslatedName ?? string.Empty)).ToList(),
        TotalBookmarks,
        CreateDateOffset.ToUnixTimeSeconds(),
        Width,
        Height,
        XRestrict,
        IsAiGenerated ? 1 : 0,
        Type == IllustrationType.Ugoira ? 2 : Type == IllustrationType.Manga ? 1 : 0);

    public const string LegacyIllustrationToken = "Mako.Model.Illustration";

    public string Serialize() => System.Text.Json.JsonSerializer.Serialize(this);

    [JsonIgnore]
    public string SerializeKey => LegacyIllustrationToken;

    private static readonly System.Text.Json.JsonSerializerOptions s_snakeCaseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly System.Text.Json.JsonSerializerOptions s_caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Illustration Deserialize(string data)
    {
        try
        {
            var res = System.Text.Json.JsonSerializer.Deserialize<Illustration>(data, s_snakeCaseOptions);
            if (res != null && (res.Id != 0 || res.ImageUrls != null))
                return res;
        }
        catch
        {
            // fallback
        }

        try
        {
            var res = System.Text.Json.JsonSerializer.Deserialize<Illustration>(data, s_caseInsensitiveOptions);
            if (res != null)
                return res;
        }
        catch
        {
            // fallback
        }

        return System.Text.Json.JsonSerializer.Deserialize<Illustration>(data)!;
    }
}
