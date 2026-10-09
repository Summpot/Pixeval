// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Controls;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.Views;
using Pixeval.Views.Capability;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class UserViewerPageViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource _loadingCts = new();

    [ObservableProperty]
    public partial bool IsFollowed { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? LoadErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Id))]
    [NotifyPropertyChangedFor(nameof(Header))]
    [NotifyPropertyChangedFor(nameof(AvatarUrl))]
    [NotifyPropertyChangedFor(nameof(BackgroundUrl))]
    [NotifyPropertyChangedFor(nameof(TabPages))]
    public partial SingleUserResponse? UserDetail { get; private set; }

    public string Header => UserDetail?.User.Name ?? Id.ToString();

    public long Id => UserDetail?.User.Id ?? field;

    public string? AvatarUrl => UserDetail?.User.AvatarUrl;

    public string? BackgroundUrl => UserDetail?.Profile.BackgroundImageUrl ?? AvatarUrl;

    public IReadOnlyList<ContentPage> TabPages => UserDetail is { User: var user }
        ?
        [
            new WorkPostsPage(user),
            new WorkBookmarksPage(user),
            new UserFollowingPage(Id),
            new UserMyPixivPage(Id),
            new RelatedUsersPage(Id),
        ]
        : [];

    public UserViewerPageViewModel(SingleUserResponse userDetail)
    {
        Id = userDetail.User.Id;
        UserDetail = userDetail;
    }

    public UserViewerPageViewModel(long userId)
    {
        Id = userId;
        _ = LoadUserAsync(userId);
    }

    partial void OnUserDetailChanged(SingleUserResponse? value)
    {
        if (value is not null)
            IsFollowed = value.User.IsFollowed;

        FollowCommand.NotifyCanExecuteChanged();
        FollowPrivatelyCommand.NotifyCanExecuteChanged();
        UnfollowCommand.NotifyCanExecuteChanged();
        BlockUserCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadUserAsync(long userId)
    {
        var token = _loadingCts.Token;

        IsLoading = true;
        LoadErrorMessage = null;
        try
        {
            var userDetail = BlockedContentHelper.Replace(
                await App.AppViewModel.MakoClient.GetUserFromIdAsync(userId, token));
            if (_disposed)
                return;

            UserDetail = userDetail;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                LoadErrorMessage = e.Message;
        }
        finally
        {
            if (!token.IsCancellationRequested && !_disposed)
                IsLoading = false;
        }
    }

    private bool CanFollow => Id != PixevalSettings.MyId;

    private bool CanBlockUser => UserDetail is { UserEntity: var user }
                                 && !BlockedContentHelper.IsBlocked(user);

    [RelayCommand(CanExecute = nameof(CanBlockUser))]
    private void BlockUser()
    {
        if (UserDetail is not { UserEntity: var user }
            || !BlockedContentHelper.TryAddOrUpdateBlockedUser(user))
            return;

        UserDetail = BlockedContentHelper.Replace(UserDetail);
    }

    [RelayCommand(CanExecute = nameof(CanFollow))]
    private async Task FollowAsync()
    {
        var result = await MakoHelper.SetFollowAsync(Id, true, false);
        if (result)
        {
            if (UserDetail?.User is { } user)
                UserUiStateStore.UpdateFollow(user, HeartButtonState.Checked);
            IsFollowed = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanFollow))]
    private async Task FollowPrivatelyAsync()
    {
        var result = await MakoHelper.SetFollowAsync(Id, true, true);
        if (result)
        {
            if (UserDetail?.User is { } user)
                UserUiStateStore.UpdateFollow(user, HeartButtonState.Checked);
            IsFollowed = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanFollow))]
    private async Task UnfollowAsync()
    {
        var result = await MakoHelper.SetFollowAsync(Id, false);
        if (result)
        {
            if (UserDetail?.User is { } user)
                UserUiStateStore.UpdateFollow(user, HeartButtonState.Unchecked);
            IsFollowed = false;
        }
    }

    #region Dispose

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        IsLoading = false;
        _loadingCts.Cancel();
        _loadingCts.Dispose();
    }

    #endregion
}
