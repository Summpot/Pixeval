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

    public static IAsyncRelayCommand<IWorkEntry> BookmarkCommand => ActionService.BookmarkCommand;

    public static IAsyncRelayCommand<BookmarkRequest> AddToBookmarkCommand => ActionService.AddToBookmarkCommand;

    public static IRelayCommand<object> AddToWatchLaterCommand => ActionService.AddToWatchLaterCommand;

    public static IAsyncRelayCommand<Illustration> SaveIllustrationCommand => ActionService.SaveIllustrationCommand;

    public static IAsyncRelayCommand<Mako.Novel> SaveNovelCommand => ActionService.SaveNovelCommand;

    public static IAsyncRelayCommand<object> SaveCommand => ActionService.SaveCommand;

    public static IAsyncRelayCommand<Image> CopyCommand => ActionService.CopyCommand;

    public static IAsyncRelayCommand<User> FollowUserCommand => ActionService.FollowUserCommand;

    public static IRelayCommand<User> BlockUserCommand => ActionService.BlockUserCommand;

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
