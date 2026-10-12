// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Pixiv;
using Pixeval.Mako;

namespace Pixeval.ViewModels.Works;

public static class WorkCommands
{
    private static IArtworkActionService ActionService =>
        App.Services?.GetService<IArtworkActionService>()
        ?? NullArtworkActionService.Instance;

    private static INavigationService NavigationService =>
        App.Services?.GetService<INavigationService>()
        ?? new NavigationService();

    public static IRelayCommand<object> OpenUserInfoPageCommand { get; } = new RelayCommand<object>(param =>
    {
        if (param is IWorkEntry { User.Id: var id } && id > 0)
            NavigationService.NavigateToUser(id);
        else if (param is Series { User.Id: var seriesUserId } && seriesUserId > 0)
            NavigationService.NavigateToUser(seriesUserId);
        else if (param is User { Id: var userId } && userId > 0)
            NavigationService.NavigateToUser(userId);
    });

    public static IRelayCommand<IWorkEntry> OpenSeriesPageCommand { get; } = new RelayCommand<IWorkEntry>(work =>
    {
        var targetWork = work is WorkEntry we ? we.AsWorkEntry : work;
        var series = targetWork?.Series;
        if (series is not { Id: > 0 })
            return;
        var workType = targetWork is Mako.Novel ? SimpleWorkType.Novel : SimpleWorkType.Illustration;
        NavigationService.NavigateToSeries(workType, series.Id);
    });

    public static IAsyncRelayCommand<IWorkEntry> BookmarkCommand { get; } = new AsyncRelayCommand<IWorkEntry>(async work =>
    {
        if (work is not null)
            await ActionService.ToggleBookmarkAsync(work);
    });

    public static IAsyncRelayCommand<BookmarkRequest> AddToBookmarkCommand { get; } = new AsyncRelayCommand<BookmarkRequest>(async req =>
    {
        if (req.Work is not null)
            await ActionService.AddToBookmarkAsync(req.Work, req.IsPrivate, req.Tags);
    });

    public static IRelayCommand<object> AddToWatchLaterCommand { get; } = new RelayCommand<object>(work =>
    {
        if (work is not null)
            ActionService.ToggleWatchLater(work);
    });

    public static IAsyncRelayCommand<Illustration> SaveIllustrationCommand { get; } = new AsyncRelayCommand<Illustration>(async illust =>
    {
        if (illust is not null)
            await ActionService.SaveIllustrationAsync(illust);
    });

    public static IAsyncRelayCommand<Mako.Novel> SaveNovelCommand { get; } = new AsyncRelayCommand<Mako.Novel>(async novel =>
    {
        if (novel is not null)
            await ActionService.SaveNovelAsync(novel);
    });

    public static IAsyncRelayCommand<object> SaveCommand { get; } = new AsyncRelayCommand<object>(async work =>
    {
        if (work is not null)
            await ActionService.SaveWorkAsync(work);
    });

    public static IAsyncRelayCommand<Image> CopyCommand { get; } = new AsyncRelayCommand<Image>(async img =>
    {
        if (img is not null)
            await ActionService.CopyImageAsync(img);
    });

    public static IAsyncRelayCommand<User> FollowUserCommand { get; } = new AsyncRelayCommand<User>(async user =>
    {
        if (user is not null)
            await ActionService.ToggleFollowUserAsync(user);
    });

    public static IRelayCommand<User> BlockUserCommand { get; } = new RelayCommand<User>(user =>
    {
        if (user is not null)
            ActionService.BlockUser(user);
    });

    public static Task<bool> ToggleBookmarkAsync(IWorkEntry work) =>
        ActionService.ToggleBookmarkAsync(work);

    public static Task<bool> AddToBookmarkAsync(IWorkEntry work, bool isPrivate, IReadOnlyList<string>? tags = null) =>
        ActionService.AddToBookmarkAsync(work, isPrivate, tags);

    public static bool ToggleWatchLater(object work) =>
        ActionService.ToggleWatchLater(work);

    public static Task SaveIllustrationAsync(Illustration entry, int setIndex) =>
        ActionService.SaveIllustrationAsync(entry, setIndex);

    public static Task SaveNovelAsync(Mako.Novel entry) =>
        ActionService.SaveNovelAsync(entry);

    public static Task SaveWorkAsync(object work) =>
        ActionService.SaveWorkAsync(work);

    public static Task SaveImageAsync(object entry, object? parameter, int setIndex)
    {
        if (entry is WorkEntry we)
            entry = we.AsWorkEntry;
        if (entry is Illustration illustration)
            return SaveIllustrationAsync(illustration, setIndex);
        return Task.CompletedTask;
    }

    public static Task<bool> ToggleFollowUserAsync(User user) =>
        ActionService.ToggleFollowUserAsync(user);

    public static bool BlockUser(User user) =>
        ActionService.BlockUser(user);

    public static Task CopyImageAsync(Image? image) =>
        ActionService.CopyImageAsync(image);
}
