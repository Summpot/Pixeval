// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Misaki;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Mako;

public partial record Novel : IArtworkInfo, IWorkEntry, INovelEntry, ISerializable
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
    public int Width => 0;

    [JsonIgnore]
    public int Height => 0;

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
    public ImageType ImageType => ImageType.Other;

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
        1 => Pixeval.Models.Pixiv.XRestrict.R18,
        2 => Pixeval.Models.Pixiv.XRestrict.R18G,
        _ => Pixeval.Models.Pixiv.XRestrict.Ordinary
    };

    [JsonIgnore]
    public bool IsAiGenerated => NovelAiType == 2;

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
            if (res != null && (res.Id != 0 || res.ImageUrls != null))
                return res;
        }
        catch
        {
            // fallback
        }

        try
        {
            var res = System.Text.Json.JsonSerializer.Deserialize<Novel>(data, s_caseInsensitiveOptions);
            if (res != null)
                return res;
        }
        catch
        {
            // fallback
        }

        return System.Text.Json.JsonSerializer.Deserialize<Novel>(data)!;
    }
}
