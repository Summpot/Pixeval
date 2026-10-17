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

namespace Pixeval.Mako;

public partial record struct Illustration : IWorkEntry
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
    public IllustContentType Type => IllustType;

    [JsonIgnore]
    public int SetIndex { get; init; } = -1;

    [JsonIgnore]
    public string? OriginalUrl => MetaSinglePage.OriginalImageUrl ?? ImageUrls.Original ?? ImageUrls.Large ?? ImageUrls.Medium;

    [JsonIgnore]
    public bool IsPicGif => IllustType is IllustContentType.Ugoira;

    [JsonIgnore]
    public bool IsPicSet => PageCount > 1 || SetIndex > -1;

    [JsonIgnore]
    public bool IsPicOne => !IsPicSet && !IsPicGif;

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixiv.net/artworks/{Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://illust/{Id}");

    [JsonIgnore]
    public SafeRating SafeRating => XRestrict.ToSafeRating(RestrictionAttributes, SanityLevel);


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
        (int)StoredEnum.ToIndex(XRestrict),
        IsAiGenerated ? 1 : 0,
        Type == IllustContentType.Ugoira ? 2 : Type == IllustContentType.Manga ? 1 : 0);
}
