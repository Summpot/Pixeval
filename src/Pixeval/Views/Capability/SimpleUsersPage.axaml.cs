// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
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
        App.Services?.GetService<IUserSessionService>()?.CurrentUserId ?? PixevalSettings.MyId;

    protected SimpleUsersPage() => InitializeComponent();

    protected void InitializeSource(UserViewViewModel? viewModel = null)
    {
        if (viewModel is not null)
            UserContainer.UserView.SetViewModel(viewModel);
        else
            ResetEngine(GetFetchEngine(App.Services?.GetService<MakoClient>() ?? App.AppViewModel.MakoClient));
    }

    private void UserContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    protected void ChangeSource()
    {
        ResetEngine(GetFetchEngine(App.AppViewModel.MakoClient));
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
    private readonly long _userId;

    public UserMyPixivPage() : this(CurrentUserId)
    {
    }

    public UserMyPixivPage(long id, UserViewViewModel? viewModel = null)
    {
        _userId = id;
        InitializeSource(viewModel);
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
    private readonly long _userId;

    public RelatedUsersPage() : this(CurrentUserId)
    {
    }

    public RelatedUsersPage(long id)
    {
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
