// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.I18N;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Capability;

public abstract partial class SimpleUsersPage : IconContentPage
{
    protected static long CurrentUserId =>
        App.Services?.GetService<IUserSessionService>()?.CurrentUserId ?? 0;

    protected SimpleUsersPage() => InitializeComponent();

    protected void InitializeSource(UserViewViewModel? viewModel = null)
    {
        if (viewModel is not null)
            UserContainer.UserView.SetViewModel(viewModel);
        else
            ResetEngine(GetFetchEngine(App.Services!.GetRequiredService<MakoClient>()));
    }

    private void UserContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    protected void ChangeSource()
    {
        ResetEngine(GetFetchEngine(App.Services!.GetRequiredService<MakoClient>()));
    }

    private void ResetEngine(IAsyncEnumerable<User> fetchEngine) =>
        (UserContainer.UserView.DataContext as UserViewViewModel)?.ResetEngine(fetchEngine);

    protected abstract IAsyncEnumerable<User> GetFetchEngine(MakoClient makoClient);
}

public class UserRecommendedPage : SimpleUsersPage
{
    public UserRecommendedPage() : this(null)
    {
    }

    public UserRecommendedPage(UserViewViewModel? viewModel)
    {
        InitializeSource(viewModel);
    }

    protected override IAsyncEnumerable<User> GetFetchEngine(MakoClient makoClient)
    {
        return makoClient.UserRecommended();
    }
}

public class UserSearchResultPage : SimpleUsersPage
{
    private readonly string? _searchText;

    public UserSearchResultPage() : this(null)
    {
    }

    public UserSearchResultPage(string? searchText, UserViewViewModel? viewModel = null)
    {
        Header = I18NManager.GetResource(MainPageResources.SearchResultFormatted, searchText);
        _searchText = searchText;
        InitializeSource(viewModel);
    }

    protected override IAsyncEnumerable<User> GetFetchEngine(MakoClient makoClient)
    {
        if (_searchText is null)
            return AsyncEnumerable.Empty<User>();
        return makoClient.UserSearch(_searchText);
    }
}

public class UserFollowerPage : SimpleUsersPage
{
    public UserFollowerPage() : this(null)
    {
    }

    public UserFollowerPage(UserViewViewModel? viewModel)
    {
        InitializeSource(viewModel);
    }

    protected override IAsyncEnumerable<User> GetFetchEngine(MakoClient makoClient)
    {
        var myId = CurrentUserId;
        if (myId <= 0)
            return AsyncEnumerable.Empty<User>();
        return makoClient.UserFollower(myId);
    }
}

public class UserMyPixivPage : SimpleUsersPage
{
    public static readonly StyledProperty<long> UserIdProperty =
        AvaloniaProperty.Register<UserMyPixivPage, long>(nameof(UserId));

    private long _userId;
    private bool _hasViewModel;

    public UserMyPixivPage()
    {
    }

    public UserMyPixivPage(long id, UserViewViewModel? viewModel = null)
    {
        if (viewModel is not null)
        {
            _hasViewModel = true;
            InitializeSource(viewModel);
        }

        UserId = id;
    }

    public long UserId
    {
        get => GetValue(UserIdProperty);
        set => SetValue(UserIdProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UserIdProperty)
            ApplyUserId(change.GetNewValue<long>());
    }

    private void ApplyUserId(long id)
    {
        if (id == _userId && _userId > 0)
            return;

        _userId = id;
        if (_hasViewModel)
        {
            _hasViewModel = false;
            return;
        }

        ChangeSource();
    }

    protected override IAsyncEnumerable<User> GetFetchEngine(MakoClient makoClient)
    {
        if (_userId <= 0)
            return AsyncEnumerable.Empty<User>();
        return makoClient.UserMypixiv(_userId);
    }
}

public class RelatedUsersPage : SimpleUsersPage
{
    public static readonly StyledProperty<long> UserIdProperty =
        AvaloniaProperty.Register<RelatedUsersPage, long>(nameof(UserId));

    private long _userId;

    public RelatedUsersPage()
    {
    }

    public RelatedUsersPage(long id)
    {
        UserId = id;
    }

    public long UserId
    {
        get => GetValue(UserIdProperty);
        set => SetValue(UserIdProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UserIdProperty)
            ApplyUserId(change.GetNewValue<long>());
    }

    private void ApplyUserId(long id)
    {
        if (id == _userId && _userId > 0)
            return;

        _userId = id;
        ChangeSource();
    }

    protected override IAsyncEnumerable<User> GetFetchEngine(MakoClient makoClient)
    {
        if (_userId <= 0)
            return AsyncEnumerable.Empty<User>();
        return makoClient.UserRelated(_userId);
    }
}

public class BookmarkUsersPage : SimpleUsersPage
{
    private readonly long _workId;
    private readonly bool _isNovel;

    public BookmarkUsersPage(long workId, bool isNovel, string workTitle)
    {
        _workId = workId;
        _isNovel = isNovel;
        Header = string.IsNullOrWhiteSpace(workTitle) ? "收藏用户" : $"{workTitle} - 收藏用户";
        ChangeSource();
    }

    protected override async IAsyncEnumerable<User> GetFetchEngine(MakoClient makoClient)
    {
        if (_workId <= 0)
            yield break;

        var resp = _isNovel
            ? await makoClient.GetNovelBookmarkUsersAsync(_workId)
            : await makoClient.GetIllustBookmarkUsersAsync(_workId);

        foreach (var user in resp.Users)
            yield return user;
    }
}
