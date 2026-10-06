// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
using Avalonia.Interactivity;
using Pixeval.I18N;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Capability;

public abstract partial class SimpleUsersPage : IconContentPage
{
    protected SimpleUsersPage() => InitializeComponent();

    protected void InitializeSource(UserViewViewModel? viewModel = null)
    {
        if (viewModel is not null)
            UserContainer.UserView.SetViewModel(viewModel);
        else
            ResetEngine(GetFetchEngine(App.AppViewModel.MakoClient));
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
        (UserContainer.UserView.DataContext as UserViewViewModel)?.ResetEngine(fetchEngine, static (user, _) => new(user));

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
        if (PixevalSettings.MyId <= 0)
            return AsyncEnumerable.Empty<User>();
        return makoClient.UserFollower(PixevalSettings.MyId);
    }
}

public class UserMyPixivPage : SimpleUsersPage
{
    private readonly long _userId;

    public UserMyPixivPage() : this(PixevalSettings.MyId)
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

    public RelatedUsersPage() : this(PixevalSettings.MyId)
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
