// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Services;

public readonly record struct BookmarkRequest(IWorkEntry Work, bool IsPrivate, IReadOnlyList<string>? Tags = null)
{
    public static implicit operator BookmarkRequest((IReadOnlyList<string>? Tags, bool IsPrivate, IWorkEntry Work) tuple) =>
        new(tuple.Work, tuple.IsPrivate, tuple.Tags);
}

public interface IArtworkActionService
{
    IAsyncRelayCommand<IWorkEntry> BookmarkCommand { get; }

    IAsyncRelayCommand<BookmarkRequest> AddToBookmarkCommand { get; }

    IRelayCommand<object> AddToWatchLaterCommand { get; }

    IAsyncRelayCommand<Illustration> SaveIllustrationCommand { get; }

    IAsyncRelayCommand<Novel> SaveNovelCommand { get; }

    IAsyncRelayCommand<object> SaveCommand { get; }

    IAsyncRelayCommand<Image> CopyCommand { get; }

    IAsyncRelayCommand<User> FollowUserCommand { get; }

    IRelayCommand<User> BlockUserCommand { get; }

    Task<bool> ToggleBookmarkAsync(IWorkEntry work);

    Task<bool> AddToBookmarkAsync(IWorkEntry work, bool isPrivate, IReadOnlyList<string>? tags = null);

    bool ToggleWatchLater(object work, ViewContainerBase? viewContainer = null);

    Task SaveIllustrationAsync(Illustration illustration, int setIndex = -1, ViewContainerBase? viewContainer = null);

    Task SaveNovelAsync(Novel novel, ViewContainerBase? viewContainer = null);

    Task SaveWorkAsync(object work, ViewContainerBase? viewContainer = null);

    Task<bool> ToggleFollowUserAsync(User user);

    bool BlockUser(User user);

    Task CopyImageAsync(Image? image);
}
