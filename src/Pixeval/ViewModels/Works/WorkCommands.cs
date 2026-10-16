// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Pixiv;
using Pixeval.Mako;

namespace Pixeval.ViewModels.Works;

public sealed class WorkCommands
{
    private readonly IArtworkActionService _actionService;
    private readonly INavigationService _navigationService;

    public WorkCommands(IArtworkActionService actionService, INavigationService navigationService)
    {
        _actionService = actionService;
        _navigationService = navigationService;

        OpenUserInfoPageCommand = new RelayCommand<object>(param =>
        {
            if (param is IWorkEntry { User.Id: var id } && id > 0)
                _navigationService.NavigateToUser(id);
            else if (param is Series { User.Id: var seriesUserId } && seriesUserId > 0)
                _navigationService.NavigateToUser(seriesUserId);
            else if (param is User { Id: var userId } && userId > 0)
                _navigationService.NavigateToUser(userId);
        });

        OpenSeriesPageCommand = new RelayCommand<IWorkEntry>(work =>
        {
            var targetWork = work is WorkEntry we ? we.AsWorkEntry : work;
            var series = targetWork?.Series;
            if (series is not { Id: > 0 } s)
                return;
            var workType = targetWork is Mako.Novel ? SimpleWorkType.Novel : SimpleWorkType.Illustration;
            _navigationService.NavigateToSeries(workType, s.Id);
        });

        BookmarkCommand = new AsyncRelayCommand<IWorkEntry>(async work =>
        {
            if (work is not null)
                await _actionService.ToggleBookmarkAsync(work);
        });

        AddToBookmarkCommand = new AsyncRelayCommand<BookmarkRequest>(async req =>
        {
            if (req.Work is not null)
                await _actionService.AddToBookmarkAsync(req.Work, req.IsPrivate, req.Tags);
        });

        AddToWatchLaterCommand = new RelayCommand<object>(work =>
        {
            if (work is not null)
                _actionService.ToggleWatchLater(work);
        });

        SaveIllustrationCommand = new AsyncRelayCommand<Illustration>(async illust =>
        {
            await _actionService.SaveIllustrationAsync(illust);
        });

        SaveNovelCommand = new AsyncRelayCommand<Mako.Novel>(async novel =>
        {
            await _actionService.SaveNovelAsync(novel);
        });

        SaveCommand = new AsyncRelayCommand<object>(async work =>
        {
            if (work is not null)
                await _actionService.SaveWorkAsync(work);
        });

        CopyCommand = new AsyncRelayCommand<Image>(async img =>
        {
            if (img is not null)
                await _actionService.CopyImageAsync(img);
        });

        FollowUserCommand = new AsyncRelayCommand<User>(async user =>
        {
            await _actionService.ToggleFollowUserAsync(user);
        });

        BlockUserCommand = new RelayCommand<User>(user =>
        {
            _actionService.BlockUser(user);
        });
    }

    public IRelayCommand<object> OpenUserInfoPageCommand { get; }

    public IRelayCommand<IWorkEntry> OpenSeriesPageCommand { get; }

    public IAsyncRelayCommand<IWorkEntry> BookmarkCommand { get; }

    public IAsyncRelayCommand<BookmarkRequest> AddToBookmarkCommand { get; }

    public IRelayCommand<object> AddToWatchLaterCommand { get; }

    public IAsyncRelayCommand<Illustration> SaveIllustrationCommand { get; }

    public IAsyncRelayCommand<Mako.Novel> SaveNovelCommand { get; }

    public IAsyncRelayCommand<object> SaveCommand { get; }

    public IAsyncRelayCommand<Image> CopyCommand { get; }

    public IAsyncRelayCommand<User> FollowUserCommand { get; }

    public IRelayCommand<User> BlockUserCommand { get; }

    public Task<bool> ToggleBookmarkAsync(IWorkEntry work) =>
        _actionService.ToggleBookmarkAsync(work);

    public Task<bool> AddToBookmarkAsync(IWorkEntry work, bool isPrivate, IReadOnlyList<string>? tags = null) =>
        _actionService.AddToBookmarkAsync(work, isPrivate, tags);

    public bool ToggleWatchLater(object work) =>
        _actionService.ToggleWatchLater(work);

    public Task SaveIllustrationAsync(Illustration entry, int setIndex) =>
        _actionService.SaveIllustrationAsync(entry, setIndex);

    public Task SaveNovelAsync(Mako.Novel entry) =>
        _actionService.SaveNovelAsync(entry);

    public Task SaveWorkAsync(object work) =>
        _actionService.SaveWorkAsync(work);

    public Task SaveImageAsync(object entry, object? parameter, int setIndex)
    {
        if (entry is WorkEntry we)
            entry = we.AsWorkEntry;
        if (entry is Illustration illustration)
            return SaveIllustrationAsync(illustration, setIndex);
        return Task.CompletedTask;
    }

    public Task<bool> ToggleFollowUserAsync(User user) =>
        _actionService.ToggleFollowUserAsync(user);

    public bool BlockUser(User user) =>
        _actionService.BlockUser(user);

    public Task CopyImageAsync(Image? image) =>
        _actionService.CopyImageAsync(image);
}
