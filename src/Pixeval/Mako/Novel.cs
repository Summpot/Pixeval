// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq;
using System.Text.Json.Serialization;
using Pixeval.Filters;
using Pixeval.Pixiv;

namespace Pixeval.Mako;

public partial record struct Novel : IWorkEntry
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
    public SafeRating SafeRating => XRestrict.ToSafeRating();


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
        (int)StoredEnum.ToIndex(XRestrict),
        IsAiGenerated ? 1 : 0,
        0);
}
