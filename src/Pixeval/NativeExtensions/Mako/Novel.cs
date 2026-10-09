// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Misaki;
using Pixeval.Controls;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Native.Mako;

public partial record Novel : IArtworkInfo, IWorkEntry, INovelEntry, ISerializable, IWorkViewModel, INotifyPropertyChanged
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
    public double AspectRatio => 1;

    [JsonIgnore]
    public string? ThumbnailUrl => Thumbnails.FirstOrDefault()?.ImageUri.OriginalString ?? "";

    [JsonIgnore]
    public string Tooltip => Title;

    [JsonIgnore]
    public Novel Entry => this;

    [JsonIgnore]
    public IAsyncRelayCommand<(IReadOnlyList<string>? Tags, bool IsPrivate, object? Parameter)> AddToBookmarkCommand => WorkCommands.AddToBookmarkCommand;

    [JsonIgnore]
    public IAsyncRelayCommand<object?> BookmarkCommand => WorkCommands.BookmarkCommand;

    [JsonIgnore]
    public IRelayCommand<object?> AddToWatchLaterCommand => WorkCommands.AddToWatchLaterCommand;

    [JsonIgnore]
    public IAsyncRelayCommand<object?> SaveCommand => WorkCommands.SaveCommand;

    IArtworkInfo IWorkViewModel.Entry => this;

    private Task<NovelContent>? _loadedContentTask;

    public Task<NovelContent> GetContentAsync()
    {
        return _loadedContentTask ??= LoadContentInternalAsync();
    }

    private async Task<NovelContent> LoadContentInternalAsync()
    {
        if (BlockedContentHelper.IsBlockedPlaceholder(this))
            return BlockedContentModelHelper.CreateBlockedNovelContent(BlockedContentHelper.Replace(this));

        var content = await App.AppViewModel.MakoClient.GetNovelContentStructuredAsync(RawId);
        return content with
        {
            Title = string.IsNullOrWhiteSpace(content.Title) ? Title : content.Title,
            CoverUrl = string.IsNullOrWhiteSpace(content.CoverUrl) ? (Thumbnails.FirstOrDefault()?.ImageUri.OriginalString ?? "") : content.CoverUrl,
            UserId = content.UserId == 0 ? Author.Id : content.UserId
        };
    }

    [JsonIgnore]
    public Task<NovelContent> ContentAsync => GetContentAsync();

    private bool? _isFavorite;

    [JsonIgnore]
    public bool IsFavorite
    {
        get => _isFavorite ?? IsBookmarked;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBookmarkedDisplay));
            }
        }
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
