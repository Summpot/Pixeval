// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mako.Global.Enum;
using Mako.Net.Responses;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
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
    public partial bool IsChangingFollow { get; private set; }

    [ObservableProperty]
    public partial string? LoadErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Id))]
    [NotifyPropertyChangedFor(nameof(Header))]
    [NotifyPropertyChangedFor(nameof(AvatarUrl))]
    [NotifyPropertyChangedFor(nameof(BackgroundUrl))]
    [NotifyPropertyChangedFor(nameof(TabPages))]
    public partial SingleUserResponse? UserDetail { get; private set; }

    public string Header => UserDetail?.UserEntity.Name ?? Id.ToString();

    public long Id => UserDetail?.UserEntity.Id ?? field;

    public string? AvatarUrl => UserDetail?.UserEntity.ProfileImageUrls.Medium;

    public string? BackgroundUrl => UserDetail?.UserProfile.BackgroundImageUrl ?? AvatarUrl;

    public IReadOnlyList<ContentPage> TabPages => UserDetail is { UserEntity: var user }
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
        Id = userDetail.UserEntity.Id;
        UserDetail = BlockedContentHelper.Replace(userDetail);
    }

    public UserViewerPageViewModel(long userId)
    {
        Id = userId;
        _ = LoadUserAsync(userId);
    }

    partial void OnUserDetailChanged(SingleUserResponse? value)
    {
        if (value is not null)
            IsFollowed = value.UserEntity.IsFollowed;

        NotifyActionCommands();
    }

    partial void OnIsLoadingChanged(bool value) => NotifyActionCommands();

    partial void OnIsChangingFollowChanged(bool value) => NotifyActionCommands();

    private void NotifyActionCommands()
    {
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
            if (_disposed || token.IsCancellationRequested)
                return;

            if (userDetail.UserEntity.Id != userId)
            {
                LoadErrorMessage = I18NManager.GetResource(EntryViewerPageResources.LoadFailed);
                return;
            }

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

    private bool CanFollow => !_disposed && !IsLoading && !IsChangingFollow && UserDetail is not null && Id > 0
                             && App.AppViewModel.MakoClient.Me is { Id: var accountId } && Id != accountId;

    private bool CanBlockUser => !_disposed && !IsLoading && !IsChangingFollow && UserDetail is { UserEntity: var user }
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
    private Task FollowAsync() => SetFollowAsync(PrivacyPolicy.Public);

    [RelayCommand(CanExecute = nameof(CanFollow))]
    private Task FollowPrivatelyAsync() => SetFollowAsync(PrivacyPolicy.Private);

    [RelayCommand(CanExecute = nameof(CanFollow))]
    private Task UnfollowAsync() => SetFollowAsync(null);

    private async Task SetFollowAsync(PrivacyPolicy? privacy)
    {
        if (!CanFollow || UserDetail is not { } detail)
            return;

        var client = App.AppViewModel.MakoClient;
        var accountId = client.Me?.Id;
        var token = _loadingCts.Token;
        IsChangingFollow = true;
        try
        {
            var result = privacy is { } policy
                ? await client.PostFollowUserAsync(Id, policy, token)
                : await client.RemoveFollowUserAsync(Id, token);
            if (result && !_disposed && !token.IsCancellationRequested && client.Me?.Id == accountId
                && ReferenceEquals(UserDetail, detail))
            {
                detail.UserEntity.IsFollowed = privacy is not null;
                IsFollowed = privacy is not null;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            if (!_disposed)
                IsChangingFollow = false;
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
        IsChangingFollow = false;
        NotifyActionCommands();
        _loadingCts.Cancel();
        _loadingCts.Dispose();
    }

    #endregion
}
