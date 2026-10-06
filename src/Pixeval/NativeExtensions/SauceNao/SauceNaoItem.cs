// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Misaki;
using Pixeval.Views.Viewers;

namespace Pixeval.Native.SauceNao;

public partial record SauceNaoItem : IArtworkInfo, ISingleImage, IImageFrame, IImageSize, IIdentityInfo, ISerializable
{
    private static readonly Dictionary<string, object> s_emptyDict = [];

    [JsonIgnore]
    public int SetIndex => -1;

    public IIdentityInfo? ToIdentityInfo()
    {
        if (!string.IsNullOrWhiteSpace(ArtworkId) && !string.IsNullOrWhiteSpace(Platform))
        {
            var platformKey = Platform.ToLowerInvariant() switch
            {
                "pixiv" => IPlatformInfo.Pixiv,
                "danbooru" => IPlatformInfo.Danbooru,
                "gelbooru" => IPlatformInfo.Gelbooru,
                "yandere" => IPlatformInfo.Yandere,
                "sankaku" => IPlatformInfo.Sankaku,
                _ => Platform
            };
            return new SimpleIdentityInfo(ArtworkId, platformKey);
        }
        return null;
    }

    [JsonIgnore]
    public string RawId => ArtworkId ?? IndexId.ToString();

    string IIdentityInfo.Id => RawId;

    string IPlatformInfo.Platform => Platform;

    [JsonIgnore]
    public string TitleText => !string.IsNullOrWhiteSpace(Title) ? Title : IndexName;

    string IArtworkInfo.Title => TitleText;

    [JsonIgnore]
    public string Description => $"Similarity: {Similarity:F2}%\nSource: {SourceUrl ?? ""}";

    DateTimeOffset IArtworkInfo.CreateDate => DateTimeOffset.MinValue;

    [JsonIgnore]
    public int TotalFavorite => -1;

    [JsonIgnore]
    public int TotalView => -1;

    [JsonIgnore]
    public bool IsFavorite { get; set; }

    [JsonIgnore]
    public bool IsAiGenerated => false;

    [JsonIgnore]
    public SafeRating SafeRating => IsNsfw ? SafeRating.Explicit : SafeRating.General;

    [JsonIgnore]
    public ImageType ImageType => ImageType.SingleImage;

    [JsonIgnore]
    public Uri WebsiteUri => new(SourceUrl ?? ExtUrls.FirstOrDefault() ?? "about:blank");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://saucenao/{RawId}");

    [JsonIgnore]
    public IPreloadableList<IUser> Authors => !string.IsNullOrWhiteSpace(AuthorName) ? [new SauceNaoAuthor(AuthorName, AuthorUrl)] : [];

    [JsonIgnore]
    public IPreloadableList<IUser> Uploaders => Authors;

    ILookup<ITagCategory, ITag> IArtworkInfo.Tags => Array.Empty<ITag>().ToLookup(_ => ITagCategory.Empty);

    [JsonIgnore]
    public IReadOnlyCollection<IImageFrame> Thumbnails =>
    [
        new ImageFrame(new ImageSize(150, 150))
        {
            ImageUri = new Uri(string.IsNullOrWhiteSpace(ThumbnailUrl) ? "about:blank" : ThumbnailUrl)
        }
    ];

    [JsonIgnore]
    public IReadOnlyDictionary<string, object> AdditionalInfo => s_emptyDict;

    int IImageSize.Width => 0;

    int IImageSize.Height => 0;

    ulong IImageFrame.ByteSize => 0;

    Uri IImageFrame.ImageUri => new(string.IsNullOrWhiteSpace(ThumbnailUrl) ? "about:blank" : ThumbnailUrl);

    public const string SauceNaoItemToken = "Pixeval.Native.SauceNao.SauceNaoItem";

    [JsonIgnore]
    public string SerializeKey => SauceNaoItemToken;

    public string Serialize() => JsonSerializer.Serialize(this);

    public static SauceNaoItem Deserialize(string data) => JsonSerializer.Deserialize<SauceNaoItem>(data)!;
}

public sealed record SauceNaoAuthor(string Name, string? Url) : IUser, IIdEntry
{
    private static readonly Dictionary<string, Uri> s_emptyContact = [];
    private static readonly Dictionary<string, object> s_emptyDict = [];

    public string Id => Name;

    long IIdEntry.Id => 0;

    string IIdentityInfo.Id => Name;

    string IPlatformInfo.Platform => "saucenao";

    public string Description => "";

    public Uri WebsiteUri => new(string.IsNullOrWhiteSpace(Url) ? "about:blank" : Url);

    public Uri? AppUri => null;

    public IReadOnlyCollection<IImageFrame> Avatar => [];

    public IReadOnlyDictionary<string, Uri> ContactInformation => s_emptyContact;

    public IReadOnlyDictionary<string, object> AdditionalInfo => s_emptyDict;
}
