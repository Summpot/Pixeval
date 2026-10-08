// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Misaki;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Models.Download;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using System.Linq;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.ViewContainers;
using Pixeval.Views.Viewers;

namespace Pixeval.ViewModels;

public static class WorkCommands
{
    public static IAsyncRelayCommand<object?> BookmarkCommand { get; } =
        new AsyncRelayCommand<object?>(ExecuteBookmarkAsync);

    public static IAsyncRelayCommand<(IReadOnlyList<string>? Tags, bool IsPrivate, object? Parameter)> AddToBookmarkCommand { get; } =
        new AsyncRelayCommand<(IReadOnlyList<string>? Tags, bool IsPrivate, object? Parameter)>(ExecuteAddToBookmarkAsync);

    public static IRelayCommand<object?> AddToWatchLaterCommand { get; } =
        new RelayCommand<object?>(ExecuteAddToWatchLater);

    public static IAsyncRelayCommand<object?> SaveCommand { get; } =
        new AsyncRelayCommand<object?>(ExecuteSaveAsync);

    public static IAsyncRelayCommand<Image?> CopyCommand { get; } =
        new AsyncRelayCommand<Image?>(ExecuteCopyAsync);

    internal static IWorkViewModel? ResolveWork(object? parameter)
    {
        return parameter switch
        {
            IWorkViewModel vm => vm,
            IllustrationViewerPageViewModel viewerVm => viewerVm.CurrentIllustration,
            NovelViewerPageViewModel novelVm => novelVm.CurrentNovel,
            IllustrationViewerInfoPane pane => pane.DataContext as IllustrationViewerPageViewModel is { CurrentIllustration: { } illust } ? illust : null,
            NovelViewerPage page => page.DataContext as NovelViewerPageViewModel is { CurrentNovel: { } novel } ? novel : null,
            Control { DataContext: IWorkViewModel vm } => vm,
            Control { DataContext: IllustrationViewerPageViewModel viewerVm } => viewerVm.CurrentIllustration,
            Control { DataContext: NovelViewerPageViewModel novelVm } => novelVm.CurrentNovel,
            _ => null
        };
    }

    private static ViewContainerBase? ResolveViewContainer(object? parameter)
    {
        if (parameter is Control control && TopLevel.GetTopLevel(control)?.ViewContainer is { } vc)
            return vc;

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var activeWindow = desktop.Windows.FirstOrDefault(static w => w.IsActive) ?? desktop.MainWindow;
            if (activeWindow?.Content is ViewContainerBase windowVc)
                return windowVc;
        }

        return null;
    }

    private static async Task ExecuteBookmarkAsync(object? parameter)
    {
        if (ResolveWork(parameter) is not { } work)
            return;

        if (!work.IsBookmarkSupported || (work.IsBookmarkedDisplay & HeartButtonState.Pending) is not 0)
            return;

        work.IsBookmarkedDisplay |= HeartButtonState.Pending;
        try
        {
            var target = !work.Entry.IsFavorite;
            var result = await MakoHelper.SetWorkBookmarkAsync((IWorkEntry) work.Entry, target);
            if (result)
            {
                if (work.Entry is Illustration illust)
                    illust.IsFavorite = target;
                else if (work.Entry is Novel novel)
                    novel.IsFavorite = target;
            }

            work.IsBookmarkedDisplay = (result ? target : work.Entry.IsFavorite)
                ? HeartButtonState.Checked
                : HeartButtonState.Unchecked;
        }
        catch
        {
            work.IsBookmarkedDisplay = work.Entry.IsFavorite
                ? HeartButtonState.Checked
                : HeartButtonState.Unchecked;
            throw;
        }
    }

    private static async Task ExecuteAddToBookmarkAsync((IReadOnlyList<string>? Tags, bool IsPrivate, object? Parameter) parameter)
    {
        if (ResolveWork(parameter.Parameter) is not { } work)
            return;

        if (!work.IsBookmarkSupported || (work.IsBookmarkedDisplay & HeartButtonState.Pending) is not 0)
            return;

        work.IsBookmarkedDisplay |= HeartButtonState.Pending;
        try
        {
            var result = await MakoHelper.SetWorkBookmarkAsync((IWorkEntry) work.Entry, true, parameter.IsPrivate, parameter.Tags);
            if (result)
            {
                if (work.Entry is Illustration illust)
                    illust.IsFavorite = true;
                else if (work.Entry is Novel novel)
                    novel.IsFavorite = true;
            }

            work.IsBookmarkedDisplay = (result || work.Entry.IsFavorite)
                ? HeartButtonState.Checked
                : HeartButtonState.Unchecked;
        }
        catch
        {
            work.IsBookmarkedDisplay = work.Entry.IsFavorite
                ? HeartButtonState.Checked
                : HeartButtonState.Unchecked;
            throw;
        }
    }

    private static void ExecuteAddToWatchLater(object? parameter)
    {
        if (ResolveWork(parameter) is not { } work)
            return;

        if (App.AppViewModel is not { } app || !WatchLaterRecord.TryCreateWorkKey(work.Entry, out _))
            return;

        var target = !work.IsInWatchLater;
        if (target)
        {
            if (!app.AddWatchLater(work.Entry))
                return;
        }
        else if (!app.RemoveWatchLater(work.Entry))
        {
            return;
        }

        work.IsInWatchLater = target;
        ResolveViewContainer(parameter)?.ShowSuccess(
            I18NManager.GetResource(target ? MiscResources.AddedToWatchLater : MiscResources.RemovedFromWatchLater));
    }

    private static async Task ExecuteSaveAsync(object? parameter)
    {
        if (ResolveWork(parameter) is not { } work)
            return;

        if (BlockedContentHelper.IsBlockedPlaceholder(work.Entry))
            return;

        var viewContainer = ResolveViewContainer(parameter);
        switch (work.Entry)
        {
            case Illustration illustration:
                await SaveIllustrationAsync(viewContainer, illustration, -1);
                break;
            case Novel novel:
                await SaveNovelAsync(viewContainer, novel);
                break;
        }
    }

    public static async Task SaveImageAsync(IArtworkInfo entry, object? parameter, int setIndex)
    {
        if (BlockedContentHelper.IsBlockedPlaceholder(entry))
            return;

        if (entry is Illustration illustration)
            await SaveIllustrationAsync(ResolveViewContainer(parameter), illustration, setIndex);
    }

    public static async ValueTask SaveIllustrationAsync(ViewContainerBase? viewContainerBase, Illustration entry, int setIndex)
    {
        var path = App.AppViewModel.AppSettings.DownloadSettings.DownloadPathMacro;
        if (entry.IsPicGif && entry is ISingleAnimatedImage { MultiImageUris: not null } animatedImage)
            await animatedImage.MultiImageUris.TryPreloadListAsync(animatedImage);

        var factory = App.AppViewModel.AppServiceProvider.GetRequiredService<IllustrationDownloadTaskFactory>();
        var task = factory.Create(entry, path, setIndex);
        App.AppViewModel.DownloadManager.QueueTask(task);
        viewContainerBase?.ShowSuccess(I18NManager.GetResource(EntryItemResources.DownloadTaskCreated));
    }

    public static async ValueTask SaveNovelAsync(ViewContainerBase? viewContainerBase, Novel entry)
    {
        var path = App.AppViewModel.AppSettings.DownloadSettings.DownloadPathMacro;
        var content = await entry.GetContentAsync();
        var factory = App.AppViewModel.AppServiceProvider.GetRequiredService<NovelDownloadTaskFactory>();
        var task = factory.Create(entry, path, content);
        App.AppViewModel.DownloadManager.QueueTask(task);
        viewContainerBase?.ShowSuccess(I18NManager.GetResource(EntryItemResources.DownloadTaskCreated));
    }

    private static async Task ExecuteCopyAsync(Image? parameter)
    {
        if (parameter is not { Source: Bitmap bitmap })
            return;

        if (TopLevel.GetTopLevel(parameter) is not { Clipboard: { } clipboard } topLevel)
            return;

        await clipboard.SetBitmapAsync(bitmap);
        await clipboard.FlushAsync();
        topLevel.ViewContainer?.ShowSuccess(I18NManager.GetResource(MiscResources.Copied));
    }
}
