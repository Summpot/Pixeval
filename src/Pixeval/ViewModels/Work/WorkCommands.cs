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
using Pixeval.Utilities;
using Pixeval.Views.ViewContainers;

namespace Pixeval.ViewModels;

public static class WorkCommands
{
    public static IAsyncRelayCommand<Control?> BookmarkCommand { get; } =
        new AsyncRelayCommand<Control?>(ExecuteBookmarkAsync);

    public static IAsyncRelayCommand<(IReadOnlyList<string>? Tags, bool IsPrivate, Control? Control)> AddToBookmarkCommand { get; } =
        new AsyncRelayCommand<(IReadOnlyList<string>? Tags, bool IsPrivate, Control? Control)>(ExecuteAddToBookmarkAsync);

    public static IRelayCommand<Control?> AddToWatchLaterCommand { get; } =
        new RelayCommand<Control?>(ExecuteAddToWatchLater);

    public static IAsyncRelayCommand<Control?> SaveCommand { get; } =
        new AsyncRelayCommand<Control?>(ExecuteSaveAsync);

    public static IAsyncRelayCommand<Image?> CopyCommand { get; } =
        new AsyncRelayCommand<Image?>(ExecuteCopyAsync);

    private static IWorkViewModel? ResolveWork(object? parameter)
    {
        return parameter switch
        {
            IWorkViewModel vm => vm,
            Control control => control.DataContext as IWorkViewModel,
            _ => null
        };
    }

    private static async Task ExecuteBookmarkAsync(Control? parameter)
    {
        if (ResolveWork(parameter) is not { } work)
            return;

        if (!work.IsBookmarkSupported || (work.IsBookmarkedDisplay & HeartButtonState.Pending) is not 0)
            return;

        work.IsBookmarkedDisplay |= HeartButtonState.Pending;
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

    private static async Task ExecuteAddToBookmarkAsync((IReadOnlyList<string>? Tags, bool IsPrivate, Control? Control) parameter)
    {
        if (ResolveWork(parameter.Control) is not { } work)
            return;

        if (!work.IsBookmarkSupported || (work.IsBookmarkedDisplay & HeartButtonState.Pending) is not 0)
            return;

        work.IsBookmarkedDisplay |= HeartButtonState.Pending;
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

    private static void ExecuteAddToWatchLater(Control? parameter)
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
        TopLevel.GetTopLevel(parameter)?.ViewContainer?.ShowSuccess(
            I18NManager.GetResource(target ? MiscResources.AddedToWatchLater : MiscResources.RemovedFromWatchLater));
    }

    private static async Task ExecuteSaveAsync(Control? parameter)
    {
        if (ResolveWork(parameter) is not { } work)
            return;

        if (BlockedContentHelper.IsBlockedPlaceholder(work.Entry))
            return;

        var viewContainer = TopLevel.GetTopLevel(parameter)?.ViewContainer;
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

    public static async Task SaveImageAsync(IArtworkInfo entry, Control? parameter, int setIndex)
    {
        if (BlockedContentHelper.IsBlockedPlaceholder(entry))
            return;

        if (entry is Illustration illustration)
            await SaveIllustrationAsync(TopLevel.GetTopLevel(parameter)?.ViewContainer, illustration, setIndex);
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
