// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Pixeval.AppManagement.Settings;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Models.Download;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Utilities;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Services;

public sealed class ArtworkActionService : IArtworkActionService
{
    private readonly MakoClient _makoClient;
    private readonly StorageEngine _storageEngine;
    private readonly DownloadManager _downloadManager;
    private readonly AppSettings _appSettings;
    private readonly IllustrationDownloadTaskFactory _illustrationDownloadTaskFactory;
    private readonly NovelDownloadTaskFactory _novelDownloadTaskFactory;
    private readonly FileLogger _logger;

    public ArtworkActionService(
        MakoClient makoClient,
        StorageEngine storageEngine,
        DownloadManager downloadManager,
        AppSettings appSettings,
        IllustrationDownloadTaskFactory illustrationDownloadTaskFactory,
        NovelDownloadTaskFactory novelDownloadTaskFactory,
        FileLogger logger)
    {
        _makoClient = makoClient;
        _storageEngine = storageEngine;
        _downloadManager = downloadManager;
        _appSettings = appSettings;
        _illustrationDownloadTaskFactory = illustrationDownloadTaskFactory;
        _novelDownloadTaskFactory = novelDownloadTaskFactory;
        _logger = logger;

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

        SaveNovelCommand = new AsyncRelayCommand<Novel>(async novel =>
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

    public IAsyncRelayCommand<Novel> SaveNovelCommand { get; }

    public IAsyncRelayCommand<object> SaveCommand { get; }

    public IAsyncRelayCommand<Image> CopyCommand { get; }

    public IAsyncRelayCommand<User> FollowUserCommand { get; }

    public IRelayCommand<User> BlockUserCommand { get; }

    public async Task<bool> ToggleBookmarkAsync(IWorkEntry work)
    {
        if (BlockedContentHelper.IsBlockedPlaceholder(work))
            return false;

        var state = ArtworkUiStateStore.GetOrCreate(work);
        if ((state.BookmarkState & HeartButtonState.Pending) is not 0)
            return false;

        var currentIsFavorite = (state.BookmarkState & HeartButtonState.Checked) is not 0;
        var target = !currentIsFavorite;

        ArtworkUiStateStore.SetBookmarkPending(work);
        try
        {
            var result = await _makoClient.SetWorkBookmarkAsync(work, target);
            if (result)
            {
                ArtworkUiStateStore.SetBookmarkState(work, target);
                return true;
            }
            else
            {
                ArtworkUiStateStore.RevertBookmarkPending(work, currentIsFavorite);
                return false;
            }
        }
        catch (Exception ex)
        {
            ArtworkUiStateStore.RevertBookmarkPending(work, currentIsFavorite);
            _logger.LogError(nameof(ToggleBookmarkAsync), ex);
            throw;
        }
    }

    public async Task<bool> AddToBookmarkAsync(IWorkEntry work, bool isPrivate, IReadOnlyList<string>? tags = null)
    {
        if (BlockedContentHelper.IsBlockedPlaceholder(work))
            return false;

        var state = ArtworkUiStateStore.GetOrCreate(work);
        if ((state.BookmarkState & HeartButtonState.Pending) is not 0)
            return false;

        var currentIsFavorite = (state.BookmarkState & HeartButtonState.Checked) is not 0;

        ArtworkUiStateStore.SetBookmarkPending(work);
        try
        {
            var result = await _makoClient.SetWorkBookmarkAsync(work, true, isPrivate, tags);
            if (result)
            {
                ArtworkUiStateStore.SetBookmarkState(work, true);
                return true;
            }
            else
            {
                ArtworkUiStateStore.RevertBookmarkPending(work, currentIsFavorite);
                return false;
            }
        }
        catch (Exception ex)
        {
            ArtworkUiStateStore.RevertBookmarkPending(work, currentIsFavorite);
            _logger.LogError(nameof(AddToBookmarkAsync), ex);
            throw;
        }
    }

    public bool ToggleWatchLater(object work, ViewContainerBase? viewContainer = null)
    {
        if (!WatchLaterRecord.TryCreateWorkKey(work, out _))
            return false;

        var state = ArtworkUiStateStore.GetOrCreate(work);
        var target = !state.IsInWatchLater;
        if (target)
        {
            if (!_storageEngine.WatchLaterRepository.AddWatchLater(work))
                return false;
        }
        else if (!_storageEngine.WatchLaterRepository.RemoveWatchLater(work))
        {
            return false;
        }

        ArtworkUiStateStore.SetWatchLater(work, target);
        (viewContainer ?? ResolveActiveViewContainer())?.ShowSuccess(
            I18NManager.GetResource(target ? MiscResources.AddedToWatchLater : MiscResources.RemovedFromWatchLater));
        return true;
    }

    public async Task SaveIllustrationAsync(Illustration illustration, int setIndex = -1, ViewContainerBase? viewContainer = null)
    {
        if (BlockedContentHelper.IsBlockedPlaceholder(illustration))
            return;

        var path = _appSettings.DownloadSettings.DownloadPathMacro;
        var task = _illustrationDownloadTaskFactory.Create(illustration, path, setIndex);
        _downloadManager.QueueTask(task);
        (viewContainer ?? ResolveActiveViewContainer())?.ShowSuccess(I18NManager.GetResource(EntryItemResources.DownloadTaskCreated));
    }

    public async Task SaveNovelAsync(Novel novel, ViewContainerBase? viewContainer = null)
    {
        if (BlockedContentHelper.IsBlockedPlaceholder(novel))
            return;

        var path = _appSettings.DownloadSettings.DownloadPathMacro;
        var content = await _makoClient.GetNovelContentStructuredAsync(novel.RawId);
        var task = _novelDownloadTaskFactory.Create(novel, path, content);
        _downloadManager.QueueTask(task);
        (viewContainer ?? ResolveActiveViewContainer())?.ShowSuccess(I18NManager.GetResource(EntryItemResources.DownloadTaskCreated));
    }

    public async Task SaveWorkAsync(object work, ViewContainerBase? viewContainer = null)
    {
        switch (work)
        {
            case Illustration illustration:
                await SaveIllustrationAsync(illustration, -1, viewContainer);
                break;
            case Novel novel:
                await SaveNovelAsync(novel, viewContainer);
                break;
        }
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

        if (TopLevel.GetTopLevel(image) is not { Clipboard: { } clipboard } topLevel)
            return;

        await clipboard.SetBitmapAsync(bitmap);
        await clipboard.FlushAsync();
        topLevel.ViewContainer?.ShowSuccess(I18NManager.GetResource(MiscResources.Copied));
    }

    private static ViewContainerBase? ResolveActiveViewContainer()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var activeWindow = desktop.Windows.FirstOrDefault(static w => w.IsActive) ?? desktop.MainWindow;
            if (activeWindow?.Content is ViewContainerBase windowVc)
                return windowVc;
        }

        return null;
    }
}

public sealed class NullArtworkActionService : IArtworkActionService
{
    public static NullArtworkActionService Instance { get; } = new();

    public IAsyncRelayCommand<IWorkEntry> BookmarkCommand { get; } = new AsyncRelayCommand<IWorkEntry>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<BookmarkRequest> AddToBookmarkCommand { get; } = new AsyncRelayCommand<BookmarkRequest>(_ => Task.CompletedTask);
    public IRelayCommand<object> AddToWatchLaterCommand { get; } = new RelayCommand<object>(_ => { });
    public IAsyncRelayCommand<Illustration> SaveIllustrationCommand { get; } = new AsyncRelayCommand<Illustration>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<Novel> SaveNovelCommand { get; } = new AsyncRelayCommand<Novel>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<object> SaveCommand { get; } = new AsyncRelayCommand<object>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<Image> CopyCommand { get; } = new AsyncRelayCommand<Image>(_ => Task.CompletedTask);
    public IAsyncRelayCommand<User> FollowUserCommand { get; } = new AsyncRelayCommand<User>(_ => Task.CompletedTask);
    public IRelayCommand<User> BlockUserCommand { get; } = new RelayCommand<User>(_ => { });

    public Task<bool> ToggleBookmarkAsync(IWorkEntry work) => Task.FromResult(false);
    public Task<bool> AddToBookmarkAsync(IWorkEntry work, bool isPrivate, IReadOnlyList<string>? tags = null) => Task.FromResult(false);
    public bool ToggleWatchLater(object work, ViewContainerBase? viewContainer = null) => false;
    public Task SaveIllustrationAsync(Illustration entry, int setIndex, ViewContainerBase? viewContainer = null) => Task.CompletedTask;
    public Task SaveNovelAsync(Novel entry, ViewContainerBase? viewContainer = null) => Task.CompletedTask;
    public Task SaveWorkAsync(object work, ViewContainerBase? viewContainer = null) => Task.CompletedTask;
    public Task<bool> ToggleFollowUserAsync(User user) => Task.FromResult(false);
    public bool BlockUser(User user) => false;
    public Task CopyImageAsync(Image? image) => Task.CompletedTask;
}
