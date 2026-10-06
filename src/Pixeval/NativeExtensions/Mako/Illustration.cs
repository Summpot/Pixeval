// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Misaki;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Mako;

public partial record Illustration : IArtworkInfo, IWorkEntry, ISingleImage, ISingleAnimatedImage, IImageSet, IImageSize, ISerializable
{
    private static readonly Dictionary<string, object> s_emptyDict = [];

    private bool? _isFavorite;

    [JsonIgnore]
    public bool IsFavorite
    {
        get => _isFavorite ?? IsBookmarked;
        set => _isFavorite = value;
    }

    [JsonIgnore]
    public long RawId => Id;

    long IIdEntry.Id => Id;

    string IIdentityInfo.Id => Id == 0 ? "" : Id.ToString();

    [JsonIgnore]
    public string Platform => IPlatformInfo.Pixiv;

    [JsonIgnore]
    public string Description => Caption;

    [JsonIgnore]
    public DateTimeOffset CreateDateOffset => DateTimeOffset.TryParse(CreateDate, out var dt) ? dt : default;

    DateTimeOffset IArtworkInfo.CreateDate => CreateDateOffset;

    [JsonIgnore]
    public int TotalFavorite => (int) TotalBookmarks;

    [JsonIgnore]
    public int TotalViewCount => (int) TotalView;

    int IArtworkInfo.TotalView => TotalViewCount;

    [JsonIgnore]
    public User Author => User;

    User IWorkEntry.User => User;

    [JsonIgnore]
    public IReadOnlyList<Tag> TagList => Tags;

    ILookup<ITagCategory, ITag> IArtworkInfo.Tags => Tags.ToLookup(_ => ITagCategory.Empty, ITag (t) => t);

    [JsonIgnore]
    public IPreloadableList<IUser> Authors => [User];

    [JsonIgnore]
    public IPreloadableList<IUser> Uploaders => [];

    [JsonIgnore]
    public IReadOnlyDictionary<string, object> AdditionalInfo => s_emptyDict;

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
    public string? OriginalSingleUrl => MetaSinglePage?.OriginalImageUrl;

    [JsonIgnore]
    public bool IsPicGif => string.Equals(IllustType, "ugoira", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsPicSet => PageCount > 1 || SetIndex > -1;

    [JsonIgnore]
    public ImageType ImageType => IsPicSet
        ? ImageType.ImageSet
        : IsPicGif
            ? ImageType.SingleAnimatedImage
            : ImageType.SingleImage;

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
        1 => Pixeval.Models.Pixiv.XRestrict.R18,
        2 => Pixeval.Models.Pixiv.XRestrict.R18G,
        _ => Pixeval.Models.Pixiv.XRestrict.Ordinary
    };

    [JsonIgnore]
    public bool IsAiGenerated => IllustAiType == 2;

    [JsonIgnore]
    public IReadOnlyCollection<IImageFrame> Thumbnails
    {
        get
        {
            var med = ImageUrls?.Medium ?? ImageUrls?.SquareMedium;
            var large = ImageUrls?.Large ?? med;
            return
            [
                new ImageFrame(IImageSize.Uniform(this, 540, 540)) { ImageUri = string.IsNullOrWhiteSpace(med) ? new("about:blank") : new(med) },
                new ImageFrame(IImageSize.Uniform(this, 600, 1200)) { ImageUri = string.IsNullOrWhiteSpace(large) ? new("about:blank") : new(large) },
            ];
        }
    }

    ulong IImageFrame.ByteSize => 0;

    Uri IImageFrame.ImageUri
    {
        get
        {
            var url = OriginalSingleUrl ?? ImageUrls?.Original ?? ImageUrls?.Large ?? ImageUrls?.Medium;
            return string.IsNullOrWhiteSpace(url) ? new("about:blank") : new(url);
        }
    }

    [JsonIgnore]
    public SingleAnimatedImageType PreferredAnimatedImageType => SingleAnimatedImageType.MultiFiles;

    [JsonIgnore]
    public Uri? SingleImageUri => null;

    [JsonIgnore]
    public UgoiraMetadata? UgoiraMetadata { get; set; }

    public async Task<UgoiraMetadata> LoadUgoiraMetadataAsync(MakoClient client, CancellationToken token = default)
    {
        if (!IsPicGif)
            throw new InvalidOperationException("Not Ugoira");
        return this.UgoiraMetadata ??= await client.GetUgoiraMetadataAsync(Id);
    }

    [JsonIgnore]
    public IPreloadableList<int>? ZipImageDelays => UgoiraMetadata is { } u ? [.. u.Frames.Select(f => f.Delay)] : null;

    [JsonIgnore]
    public IPreloadableList<(Uri, int)> MultiImageUris => UgoiraMetadata is { } u && OriginalSingleUrl is { } orig
        ? [.. u.Frames.Select((f, i) => (new Uri(orig.Replace("ugoira0", $"ugoira{i}")), f.Delay))]
        : [];

    [JsonIgnore]
    public IPreloadableList<IAnimatedImageFrame> AnimatedThumbnails => UgoiraMetadata is { } u
        ? [
            new AnimatedImageFrame(IImageSize.Uniform(this, 540, 540), new Uri(u.ZipUrls.Medium), [.. u.Frames.Select(f => f.Delay)]),
            new AnimatedImageFrame(IImageSize.Uniform(this, 600, 1200), new Uri(u.ZipUrls.Medium.Replace("600x600", "1920x1080")), [.. u.Frames.Select(f => f.Delay)])
        ]
        : [];

    [JsonIgnore]
    public IPreloadableList<ISingleImage> Pages => PageCount <= 1
        ? [this]
        : [.. MetaPages.Select((m, i) => this with
        {
            SetIndex = i,
            ImageUrls = m.ImageUrls,
            MetaSinglePage = new MetaSinglePage(m.ImageUrls.Original)
        })];

    public const string LegacyIllustrationToken = "Mako.Model.Illustration";

    public string Serialize() => System.Text.Json.JsonSerializer.Serialize(this);

    [JsonIgnore]
    public string SerializeKey => LegacyIllustrationToken;

    public static Illustration Deserialize(string data) =>
        System.Text.Json.JsonSerializer.Deserialize<Illustration>(data)!;
}
