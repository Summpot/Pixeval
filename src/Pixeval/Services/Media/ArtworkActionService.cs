// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Pixiv;
using Pixeval.Booru;
using Pixeval.Download;
using Pixeval.Mako;
using Pixeval.SauceNao;
using Pixeval.Storage;
using Pixeval.Utilities;

namespace Pixeval.Services.Media;

public sealed class ArtworkActionService : IArtworkActionService
{
    private readonly MakoClient _makoClient;
    private readonly StorageEngine _storageEngine;
    private readonly DownloadManager _downloadManager;
    private readonly AppSettings _appSettings;
    private readonly FileLogger _logger;
    private readonly INotificationService _notifications;
    private readonly IClipboardService _clipboard;
    private readonly IStoragePickerService _storagePicker;

    public ArtworkActionService(
        MakoClient makoClient,
        StorageEngine storageEngine,
        DownloadManager downloadManager,
        AppSettings appSettings,
        FileLogger logger,
        INotificationService notifications,
        IClipboardService clipboard,
        IStoragePickerService storagePicker)
    {
        _makoClient = makoClient;
        _storageEngine = storageEngine;
        _downloadManager = downloadManager;
        _appSettings = appSettings;
        _logger = logger;
        _notifications = notifications;
        _clipboard = clipboard;
        _storagePicker = storagePicker;

        BookmarkCommand = new AsyncRelayCommand<IWorkEntry>(async work =>
        {
            if (work is not null)
                await ToggleBookmarkAsync(work);
        });

        AddToBookmarkCommand = new AsyncRelayCommand<BookmarkRequest>(async req =>
        {
            if (req.Work is not null)
                await AddToBookmarkAsync(req.Work, req.IsPrivate, req.Tags);
        });

        AddToWatchLaterCommand = new RelayCommand<object>(work =>
        {
            if (work is not null)
                ToggleWatchLater(work);
        });

        SaveIllustrationCommand = new AsyncRelayCommand<Illustration>(async illust =>
        {
            if (illust is not null)
                await SaveIllustrationAsync(illust);
        });

        SaveNovelCommand = new AsyncRelayCommand<Mako.Novel>(async novel =>
        {
            if (novel is not null)
                await SaveNovelAsync(novel);
        });

        SaveCommand = new AsyncRelayCommand<object>(async work =>
        {
            if (work is not null)
                await SaveWorkAsync(work);
        });

        CopyCommand = new AsyncRelayCommand<Image>(async img =>
        {
            if (img is not null)
                await CopyImageAsync(img);
        });

        FollowUserCommand = new AsyncRelayCommand<User>(async user =>
        {
            if (user is not null)
                await ToggleFollowUserAsync(user);
        });

        BlockUserCommand = new RelayCommand<User>(user =>
        {
            if (user is not null)
                BlockUser(user);
        });
    }

    public IAsyncRelayCommand<IWorkEntry> BookmarkCommand { get; }

    public IAsyncRelayCommand<BookmarkRequest> AddToBookmarkCommand { get; }

    public IRelayCommand<object> AddToWatchLaterCommand { get; }

    public IAsyncRelayCommand<Illustration> SaveIllustrationCommand { get; }

    public IAsyncRelayCommand<Mako.Novel> SaveNovelCommand { get; }

    public IAsyncRelayCommand<object> SaveCommand { get; }

    public IAsyncRelayCommand<Image> CopyCommand { get; }

    public IAsyncRelayCommand<User> FollowUserCommand { get; }

    public IRelayCommand<User> BlockUserCommand { get; }

    public async Task<bool> ToggleBookmarkAsync(IWorkEntry work)
    {
        var targetWork = work is WorkEntry we ? we.AsWorkEntry : work;
        if (BlockedContentHelper.IsBlockedPlaceholder(targetWork))
            return false;

        var state = ArtworkUiStateStore.GetOrCreate(targetWork);
        if ((state.BookmarkState & HeartButtonState.Pending) is not 0)
            return false;

        var currentIsFavorite = (state.BookmarkState & HeartButtonState.Checked) is not 0;
        var target = !currentIsFavorite;

        ArtworkUiStateStore.SetBookmarkPending(targetWork);
        try
        {
            var result = await _makoClient.SetWorkBookmarkAsync(targetWork, target);
            if (result)
            {
                ArtworkUiStateStore.SetBookmarkState(targetWork, target);
                return true;
            }
            else
            {
                ArtworkUiStateStore.RevertBookmarkPending(targetWork, currentIsFavorite);
                return false;
            }
        }
        catch (Exception ex)
        {
            ArtworkUiStateStore.RevertBookmarkPending(targetWork, currentIsFavorite);
            _logger.LogError(nameof(ToggleBookmarkAsync), ex);
            throw;
        }
    }

    public async Task<bool> AddToBookmarkAsync(IWorkEntry work, bool isPrivate, IReadOnlyList<string>? tags = null)
    {
        var targetWork = work is WorkEntry we ? we.AsWorkEntry : work;
        if (BlockedContentHelper.IsBlockedPlaceholder(targetWork))
            return false;

        var state = ArtworkUiStateStore.GetOrCreate(targetWork);
        if ((state.BookmarkState & HeartButtonState.Pending) is not 0)
            return false;

        var currentIsFavorite = (state.BookmarkState & HeartButtonState.Checked) is not 0;

        ArtworkUiStateStore.SetBookmarkPending(targetWork);
        try
        {
            var result = await _makoClient.SetWorkBookmarkAsync(targetWork, true, isPrivate, tags);
            if (result)
            {
                ArtworkUiStateStore.SetBookmarkState(targetWork, true);
                return true;
            }
            else
            {
                ArtworkUiStateStore.RevertBookmarkPending(targetWork, currentIsFavorite);
                return false;
            }
        }
        catch (Exception ex)
        {
            ArtworkUiStateStore.RevertBookmarkPending(targetWork, currentIsFavorite);
            _logger.LogError(nameof(AddToBookmarkAsync), ex);
            throw;
        }
    }

    public bool ToggleWatchLater(object work)
    {
        var targetWork = work is WorkEntry we ? we.AsWorkEntry : work;
        if (!WatchLaterRecord.TryCreateWorkKey(targetWork, out _))
            return false;

        var state = ArtworkUiStateStore.GetOrCreate(targetWork);
        var target = !state.IsInWatchLater;
        if (target)
        {
            if (!_storageEngine.WatchLaterRepository.AddWatchLater(targetWork))
                return false;
        }
        else if (!_storageEngine.WatchLaterRepository.RemoveWatchLater(targetWork))
        {
            return false;
        }

        ArtworkUiStateStore.SetWatchLater(targetWork, target);
        _notifications.ShowSuccess(
            I18NManager.GetResource(target ? MiscResources.AddedToWatchLater : MiscResources.RemovedFromWatchLater));
        return true;
    }

    public Task SaveIllustrationAsync(Illustration illustration, int setIndex = -1)
    {
        if (BlockedContentHelper.IsBlockedPlaceholder(illustration))
            return Task.CompletedTask;

        var destination = _downloadManager.EnqueuePixiv(
            illustration.Serialize(),
            false,
            setIndex,
            _appSettings.DownloadSettings.DownloadPathMacro,
            "",
            0,
            "");
        if (!string.IsNullOrEmpty(destination))
            _notifications.ShowSuccess(I18NManager.GetResource(EntryItemResources.DownloadTaskCreated));
        return Task.CompletedTask;
    }

    public Task SaveNovelAsync(Mako.Novel novel)
    {
        if (BlockedContentHelper.IsBlockedPlaceholder(novel))
            return Task.CompletedTask;

        var destination = _downloadManager.EnqueuePixiv(
            novel.Serialize(),
            true,
            -1,
            _appSettings.DownloadSettings.DownloadPathMacro,
            "",
            0,
            "");
        if (!string.IsNullOrEmpty(destination))
            _notifications.ShowSuccess(I18NManager.GetResource(EntryItemResources.DownloadTaskCreated));
        return Task.CompletedTask;
    }

    public async Task SaveWorkAsync(object work)
    {
        var targetWork = work is WorkEntry we ? we.AsWorkEntry : work;
        switch (targetWork)
        {
            case Illustration illustration:
                await SaveIllustrationAsync(illustration);
                break;
            case Mako.Novel novel:
                await SaveNovelAsync(novel);
                break;
            case BooruPost post:
                EnqueueExternalImage(
                    post.Id,
                    post.Title,
                    post.UploaderName,
                    post.ThumbnailUrl ?? "",
                    post.WebsiteUri.OriginalString,
                    post.AppUri.OriginalString,
                    post.OriginalUrl ?? post.SampleUrl ?? post.PreviewUrl ?? "",
                    post.Serialize(),
                    post.SerializeKey,
                    post.SafeRating.IsR18,
                    post.SafeRating.IsR18G,
                    post.CreateDateOffset.ToString("o"));
                break;
            case SauceNaoItem sauce:
                EnqueueExternalImage(
                    sauce.RawId,
                    sauce.TitleText,
                    sauce.AuthorName,
                    sauce.ThumbnailUrl,
                    sauce.WebsiteUri.OriginalString,
                    sauce.AppUri.OriginalString,
                    string.IsNullOrWhiteSpace(sauce.ThumbnailUrl) ? "" : sauce.ThumbnailUrl,
                    sauce.Serialize(),
                    sauce.SerializeKey,
                    sauce.SafeRating.IsR18,
                    sauce.SafeRating.IsR18G,
                    "");
                break;
        }
    }

    private void EnqueueExternalImage(
        string artworkId,
        string title,
        string author,
        string thumbnailUrl,
        string websiteUri,
        string appUri,
        string originalUrl,
        string payloadJson,
        string serializeKey,
        bool isR18,
        bool isR18G,
        string createDate)
    {
        var destination = _downloadManager.EnqueueExternalImage(new ExternalImageRequest(
            artworkId,
            title,
            author,
            thumbnailUrl,
            websiteUri,
            appUri,
            originalUrl,
            _appSettings.DownloadSettings.DownloadPathMacro,
            "",
            payloadJson,
            serializeKey,
            0,
            "",
            isR18,
            isR18G,
            createDate));
        if (!string.IsNullOrEmpty(destination))
            _notifications.ShowSuccess(I18NManager.GetResource(EntryItemResources.DownloadTaskCreated));
    }

    public async Task<bool> ToggleFollowUserAsync(User user)
    {
        var state = UserUiStateStore.GetOrCreate(user);
        if ((state.FollowState & HeartButtonState.Pending) is not 0)
            return false;

        var currentFollow = (state.FollowState & HeartButtonState.Checked) is not 0;
        var target = !currentFollow;

        UserUiStateStore.SetFollowPending(user);
        try
        {
            var result = await _makoClient.SetFollowAsync(user, target);
            if (result)
            {
                UserUiStateStore.SetFollowState(user, target);
                return true;
            }
            else
            {
                UserUiStateStore.RevertFollowPending(user, currentFollow);
                return false;
            }
        }
        catch (Exception ex)
        {
            UserUiStateStore.RevertFollowPending(user, currentFollow);
            _logger.LogError(nameof(ToggleFollowUserAsync), ex);
            throw;
        }
    }

    public bool BlockUser(User user) => BlockedContentHelper.TryAddOrUpdateBlockedUser(user);

    public async Task CopyImageAsync(Image? image)
    {
        if (image is not { Source: Bitmap bitmap })
            return;

        await CopyBitmapAsync(bitmap);
    }

    public async Task CopyBitmapAsync(Bitmap bitmap)
    {
        await _clipboard.SetBitmapAsync(bitmap);
        await _clipboard.FlushAsync();
        _notifications.ShowSuccess(I18NManager.GetResource(MiscResources.Copied));
    }

    public async Task SaveBitmapAsAsync(Bitmap bitmap, string suggestedFileName)
    {
        var file = await _storagePicker.SaveFileAsync(new()
        {
            FileTypeChoices =
            [
                new("PNG")
                {
                    Patterns = ["*.png"],
                    MimeTypes = ["image/png"]
                }
            ],
            DefaultExtension = "png",
            SuggestedFileName = suggestedFileName
        });

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        _notifications.ShowSuccess(I18NManager.GetResource(MiscResources.Saved), file.Path.OriginalString);
    }
}

public sealed class NullArtworkActionService : IArtworkActionService
{
    public static NullArtworkActionService Instance { get; } = new();

    public IAsyncRelayCommand<IWorkEntry> BookmarkCommand { get; } = new AsyncRelayCommand<IWorkEntry>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<BookmarkRequest> AddToBookmarkCommand { get; } = new AsyncRelayCommand<BookmarkRequest>(_ => Task.CompletedTask);
    public IRelayCommand<object> AddToWatchLaterCommand { get; } = new RelayCommand<object>(_ => { });
    public IAsyncRelayCommand<Illustration> SaveIllustrationCommand { get; } = new AsyncRelayCommand<Illustration>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<Mako.Novel> SaveNovelCommand { get; } = new AsyncRelayCommand<Mako.Novel>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<object> SaveCommand { get; } = new AsyncRelayCommand<object>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<Image> CopyCommand { get; } = new AsyncRelayCommand<Image>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<User> FollowUserCommand { get; } = new AsyncRelayCommand<User>(_ => Task.CompletedTask);
    public IRelayCommand<User> BlockUserCommand { get; } = new RelayCommand<User>(_ => { });

    public Task<bool> ToggleBookmarkAsync(IWorkEntry work) => Task.FromResult(false);
    public Task<bool> AddToBookmarkAsync(IWorkEntry work, bool isPrivate, IReadOnlyList<string>? tags = null) => Task.FromResult(false);
    public bool ToggleWatchLater(object work) => false;
    public Task SaveIllustrationAsync(Illustration entry, int setIndex = -1) => Task.CompletedTask;
    public Task SaveNovelAsync(Mako.Novel entry) => Task.CompletedTask;
    public Task SaveWorkAsync(object work) => Task.CompletedTask;
    public Task<bool> ToggleFollowUserAsync(User user) => Task.FromResult(false);
    public bool BlockUser(User user) => false;
    public Task CopyImageAsync(Image? image) => Task.CompletedTask;
    public Task CopyBitmapAsync(Bitmap bitmap) => Task.CompletedTask;
    public Task SaveBitmapAsAsync(Bitmap bitmap, string suggestedFileName) => Task.CompletedTask;
}
