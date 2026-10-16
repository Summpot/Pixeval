// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Interactivity;
using Pixeval.Localization;
using Pixeval.Pixiv;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public abstract partial class SimpleUsersPage : IconContentPage
{
    private MakoClient? _client;
    private IUserSessionService? _session;
    private bool _sourcePending;

    protected SimpleUsersPage(MakoClient? client, IUserSessionService? session)
    {
        _client = client;
        _session = session;
        InitializeComponent();
    }

    protected long CurrentUserId => EnsureServices() ? _session!.CurrentUserId : 0;

    private bool EnsureServices()
    {
        _client ??= this.GetUiService<MakoClient>();
        _session ??= this.GetUiService<IUserSessionService>();
        return _client is not null && _session is not null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UiServices.ProviderProperty && _sourcePending)
            ChangeSource();
    }

    protected void InitializeSource(UserViewViewModel? viewModel = null)
    {
        if (viewModel is not null)
            UserContainer.UserView.SetViewModel(viewModel);
        else
            ChangeSource();
    }

    private void UserContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    protected void ChangeSource()
    {
        if (!(_sourcePending = !EnsureServices()))
            ResetEngine(GetFetchEngine(_client!));
    }

    private void ResetEngine(IAsyncEnumerable<User> fetchEngine) =>
        (UserContainer.UserView.DataContext as UserViewViewModel)?.ResetEngine(fetchEngine);

    protected abstract IAsyncEnumerable<User> GetFetchEngine(MakoClient makoClient);
}

public class UserRecommendedPage : SimpleUsersPage
{
    public UserRecommendedPage(MakoClient client, IUserSessionService session) : base(client, session)
    {
        InitializeSource();
    }

    public UserRecommendedPage(UserViewViewModel viewModel, MakoClient client, IUserSessionService session) : base(client, session)
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

    public UserSearchResultPage(MakoClient client, IUserSessionService session) : this(null, client, session)
    {
    }

    public UserSearchResultPage(string? searchText, MakoClient client, IUserSessionService session, UserViewViewModel? viewModel = null)
        : base(client, session)
    {
        Header = Strings.MainPageSearchResultFormatted(searchText: searchText);
        var languageSubscription = AppLocalization.Instance.Subscribe(() => Header = Strings.MainPageSearchResultFormatted(searchText: searchText));
        Pixeval.ViewModels.ViewModelDisposal.Register(this, languageSubscription);
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
    public UserFollowerPage(MakoClient client, IUserSessionService session) : base(client, session)
    {
        InitializeSource();
    }

    public UserFollowerPage(UserViewViewModel viewModel, MakoClient client, IUserSessionService session) : base(client, session)
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

    /// <summary>XAML constructor: services are resolved from <see cref="UiServices.ProviderProperty"/> once the page is attached under the shell.</summary>
    public UserMyPixivPage() : base(null, null)
    {
    }

    public UserMyPixivPage(long id, MakoClient client, IUserSessionService session, UserViewViewModel? viewModel = null)
        : base(client, session)
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

    /// <summary>XAML constructor: services are resolved from <see cref="UiServices.ProviderProperty"/> once the page is attached under the shell.</summary>
    public RelatedUsersPage() : base(null, null)
    {
    }

    public RelatedUsersPage(long id, MakoClient client, IUserSessionService session) : base(client, session)
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

    public BookmarkUsersPage(long workId, bool isNovel, string workTitle, MakoClient client, IUserSessionService session)
        : base(client, session)
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
