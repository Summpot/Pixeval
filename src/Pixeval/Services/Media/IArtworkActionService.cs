// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Pixiv;
using Pixeval.Mako;

namespace Pixeval.Services.Media;

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

    IAsyncRelayCommand<Mako.Novel> SaveNovelCommand { get; }

    IAsyncRelayCommand<object> SaveCommand { get; }

    IAsyncRelayCommand<Image> CopyCommand { get; }

    IAsyncRelayCommand<User> FollowUserCommand { get; }

    IRelayCommand<User> BlockUserCommand { get; }

    Task<bool> ToggleBookmarkAsync(IWorkEntry work);

    Task<bool> AddToBookmarkAsync(IWorkEntry work, bool isPrivate, IReadOnlyList<string>? tags = null);

    bool ToggleWatchLater(object work);

    Task SaveIllustrationAsync(Illustration illustration, int setIndex = -1);

    Task SaveNovelAsync(Mako.Novel novel);

    Task SaveWorkAsync(object work);

    Task<bool> ToggleFollowUserAsync(User user);

    Task<bool> SetFollowUserAsync(User user, bool followed, bool privately = false, CancellationToken token = default);

    bool BlockUser(User user);

    Task CopyImageAsync(Image? image);

    Task CopyBitmapAsync(Bitmap bitmap);

    Task SaveBitmapAsAsync(Bitmap bitmap, string suggestedFileName);
}
