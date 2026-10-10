// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Views.ViewContainers;

namespace Pixeval.ViewModels;

public static class WorkCommands
{
    private static IArtworkActionService ActionService =>
        App.Services?.GetService<IArtworkActionService>()
        ?? NullArtworkActionService.Instance;

    public static IAsyncRelayCommand<IWorkEntry> BookmarkCommand => ActionService.BookmarkCommand;

    public static IAsyncRelayCommand<BookmarkRequest> AddToBookmarkCommand => ActionService.AddToBookmarkCommand;

    public static IRelayCommand<object> AddToWatchLaterCommand => ActionService.AddToWatchLaterCommand;

    public static IAsyncRelayCommand<Illustration> SaveIllustrationCommand => ActionService.SaveIllustrationCommand;

    public static IAsyncRelayCommand<Novel> SaveNovelCommand => ActionService.SaveNovelCommand;

    public static IAsyncRelayCommand<object> SaveCommand => ActionService.SaveCommand;

    public static IAsyncRelayCommand<Image> CopyCommand => ActionService.CopyCommand;

    public static IAsyncRelayCommand<User> FollowUserCommand => ActionService.FollowUserCommand;

    public static IRelayCommand<User> BlockUserCommand => ActionService.BlockUserCommand;

    public static Task<bool> ToggleBookmarkAsync(IWorkEntry work) =>
        ActionService.ToggleBookmarkAsync(work);

    public static Task<bool> AddToBookmarkAsync(IWorkEntry work, bool isPrivate, IReadOnlyList<string>? tags = null) =>
        ActionService.AddToBookmarkAsync(work, isPrivate, tags);

    public static bool ToggleWatchLater(object work, ViewContainerBase? viewContainer = null) =>
        ActionService.ToggleWatchLater(work, viewContainer);

    public static Task SaveIllustrationAsync(ViewContainerBase? viewContainerBase, Illustration entry, int setIndex) =>
        ActionService.SaveIllustrationAsync(entry, setIndex, viewContainerBase);

    public static Task SaveNovelAsync(ViewContainerBase? viewContainerBase, Novel entry) =>
        ActionService.SaveNovelAsync(entry, viewContainerBase);

    public static Task SaveWorkAsync(object work, ViewContainerBase? viewContainer = null) =>
        ActionService.SaveWorkAsync(work, viewContainer);

    public static Task SaveImageAsync(object entry, object? parameter, int setIndex)
    {
        if (entry is Illustration illustration)
            return SaveIllustrationAsync(null, illustration, setIndex);
        return Task.CompletedTask;
    }

    public static Task<bool> ToggleFollowUserAsync(User user) =>
        ActionService.ToggleFollowUserAsync(user);

    public static bool BlockUser(User user) =>
        ActionService.BlockUser(user);

    public static Task CopyImageAsync(Image? image) =>
        ActionService.CopyImageAsync(image);
}
