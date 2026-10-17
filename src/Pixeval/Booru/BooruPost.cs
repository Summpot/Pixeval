// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq;
using System.Text.Json.Serialization;
using Pixeval.Controls;
using Pixeval.Filters;
using Pixeval.Pixiv;
using Pixeval.ViewModels;

namespace Pixeval.Booru;

public partial record struct BooruPost
{
    [JsonIgnore]
    public bool IsBookmarkSupported => false;

    [JsonIgnore]
    public bool HasSeries => false;

    [JsonIgnore]
    public double AspectRatio => Width > 0 && Height > 0 ? (double) Width / Height : 1;

    [JsonIgnore]
    public string? ThumbnailUrl => PreviewUrl ?? SampleUrl ?? OriginalUrl;

    [JsonIgnore]
    public string? LargeFileUrl => SampleUrl ?? OriginalUrl;

    [JsonIgnore]
    public string? PreviewFileUrl => PreviewUrl ?? SampleUrl;

    [JsonIgnore]
    public string Tooltip => Title;

    [JsonIgnore]
    public int SetIndex => -1;

    [JsonIgnore]
    public string PlatformName => Platform.ToPlatformString();

    [JsonIgnore]
    public string Title => Source ?? "";

    [JsonIgnore]
    public string Description => "";

    [JsonIgnore]
    public DateTimeOffset CreateDateOffset => DateTimeOffset.TryParse(CreatedAt, out var dt) ? dt : default;

    [JsonIgnore]
    public int TotalViewCount => -1;

    [JsonIgnore]
    public bool IsFavorite => false;

    [JsonIgnore]
    public bool IsAiGenerated => false;

    [JsonIgnore]
    public SafeRating SafeRating => Rating.ToLowerInvariant() switch
    {
        "general" or "safe" => SafeRating.General,
        "sensitive" => SafeRating.Questionable,
        "questionable" => SafeRating.Questionable,
        "explicit" => SafeRating.Explicit,
        _ => SafeRating.NotSpecified
    };

    [JsonIgnore]
    public Uri WebsiteUri => new(Platform switch
    {
        BooruPlatform.Danbooru => $"https://danbooru.donmai.us/posts/{Id}",
        BooruPlatform.Gelbooru => $"https://gelbooru.com/index.php?page=post&s=view&id={Id}",
        BooruPlatform.Sankaku => $"https://chan.sankakucomplex.com/posts/show/{Id}",
        BooruPlatform.Yandere => $"https://yande.re/post/show/{Id}",
        BooruPlatform.Rule34 => $"https://rule34.xxx/index.php?page=post&s=view&id={Id}",
        _ => "about:blank"
    });

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://{Platform.ToPlatformString()}/{Id}");

    public ArtworkMetadata ToArtworkMetadata() => new(
        Id,
        Title,
        UploaderName ?? string.Empty,
        string.Empty,
        Tags.Select(t => new ArtworkTag(t.Name, string.Empty)).ToList(),
        Score,
        CreateDateOffset.ToUnixTimeSeconds(),
        (int) Width,
        (int) Height,
        SafeRating.IsR18G ? 2 : SafeRating.IsR18 ? 1 : 0,
        0,
        0);
}
