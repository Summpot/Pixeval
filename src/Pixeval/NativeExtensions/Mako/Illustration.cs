// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Misaki;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.ViewModels;

namespace Pixeval.Native.Mako;

public partial record Illustration : IArtworkInfo, IWorkEntry, ISingleImage, ISingleAnimatedImage, IImageSet, IImageSize, ISerializable, IWorkViewModel, INotifyPropertyChanged
{
    private static readonly Dictionary<string, object> s_emptyDict = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private HeartButtonState? _isBookmarkedDisplay;

    [JsonIgnore]
    public HeartButtonState IsBookmarkedDisplay
    {
        get => _isBookmarkedDisplay ?? (IsFavorite ? HeartButtonState.Checked : HeartButtonState.Unchecked);
        set
        {
            if (_isBookmarkedDisplay != value)
            {
                _isBookmarkedDisplay = value;
                OnPropertyChanged();
            }
        }
    }

    private bool? _isInWatchLater;

    [JsonIgnore]
    public bool IsInWatchLater
    {
        get => _isInWatchLater ?? (App.AppViewModel?.ContainsWatchLater(this) is true);
        set
        {
            if (_isInWatchLater != value)
            {
                _isInWatchLater = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public bool IsBookmarkSupported => !BlockedContentHelper.IsBlockedPlaceholder(this) && Platform is IPlatformInfo.Pixiv;

    [JsonIgnore]
    public bool HasSeries => Series is not null;

    [JsonIgnore]
    public double AspectRatio => Width > 0 && Height > 0 ? (double) Width / Height : 1;

    [JsonIgnore]
    public string? SizeText => Width > 0 && Height > 0 ? $"{Width} x {Height}" : null;

    [JsonIgnore]
    public string? ThumbnailUrl => Thumbnails.PickClosestHeight(300)?.ImageUri.OriginalString;

    [JsonIgnore]
    public string Tooltip
    {
        get
        {
            var sb = new StringBuilder(Title);
            if (IsPicGif)
                sb.AppendLine().Append(I18NManager.GetResource(EntryItemResources.TheIllustrationIsAnUgoira));
            else if (IsPicSet)
                sb.AppendLine().Append(string.Format(I18NManager.GetResource(EntryItemResources.TheIllustrationIsAMangaFormatted), PageCount));
            return sb.ToString();
        }
    }

    [JsonIgnore]
    public Illustration Entry => this;

    [JsonIgnore]
    public IAsyncRelayCommand<(IReadOnlyList<string>? Tags, bool IsPrivate, Control? Control)> AddToBookmarkCommand => WorkCommands.AddToBookmarkCommand;

    [JsonIgnore]
    public IAsyncRelayCommand<Control?> BookmarkCommand => WorkCommands.BookmarkCommand;

    [JsonIgnore]
    public IRelayCommand<Control?> AddToWatchLaterCommand => WorkCommands.AddToWatchLaterCommand;

    [JsonIgnore]
    public IAsyncRelayCommand<Control?> SaveCommand => WorkCommands.SaveCommand;

    [JsonIgnore]
    public IAsyncRelayCommand<Image?> CopyCommand => WorkCommands.CopyCommand;

    IArtworkInfo IWorkViewModel.Entry => this;

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
    public bool IsPicOne => !IsPicSet && !IsPicGif;

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
