// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Controls;
using Pixeval.Config;
using Pixeval.Pixiv;
using Pixeval.Services.Background;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Works;

namespace Pixeval.Views.Works;

public partial class WorkBookmarksPage : IconContentPage
{
    public static readonly StyledProperty<User?> UserProperty =
        AvaloniaProperty.Register<WorkBookmarksPage, User?>(nameof(User));

    private static User EmptyUser() =>
        new(0, "", "", new ProfileImageUrls(null, null, null, null), false, null, []);

    private MakoClient? _client;
    private IWorkSubscriptionService? _subscriptions;
    private StorageEngine? _storage;
    private IUserSessionService? _session;
    private FileLogger? _logger;
    private bool _userPending;
    private User _user = EmptyUser();
    private readonly string? _initialTag;
    private IWorkViewViewModel? _viewModel;
    private bool _suppressChangeSource;
    private bool _initialized;

    public static IReadOnlyList<BookmarkTag> DefaultTags { get; } = [AllBookmarkTag.Instance, UncategorizedBookmarkTag.Instance];

    public IAsyncRelayCommand AddSubscriptionCommand { get; }

    public IAsyncRelayCommand RemoveSubscriptionCommand { get; }

    /// <summary>XAML constructor: services are resolved from <see cref="UiServices.ProviderProperty"/> once the page is attached under the shell.</summary>
    public WorkBookmarksPage()
    {
        AddSubscriptionCommand = new AsyncRelayCommand(AddSubscriptionAsync);
        RemoveSubscriptionCommand = new AsyncRelayCommand(RemoveSubscriptionAsync);
        InitializeComponent();
        Configure(SimpleWorkType.Illustration, PrivacyPolicy.Public, null);
    }

    public WorkBookmarksPage(
        User user,
        AppSettings settings,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage,
        IUserSessionService session,
        FileLogger logger)
        : this(user, settings.SearchSettings.DefaultSimpleWorkType, PrivacyPolicy.Public, null, client, subscriptions, storage, session, logger)
    {
    }

    public WorkBookmarksPage(
        User user,
        SimpleWorkType simpleWorkType,
        PrivacyPolicy privacyPolicy,
        string? tag,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage,
        IUserSessionService session,
        FileLogger logger,
        IWorkViewViewModel? viewModel = null)
    {
        _client = client;
        _subscriptions = subscriptions;
        _storage = storage;
        _session = session;
        _logger = logger;
        AddSubscriptionCommand = new AsyncRelayCommand(AddSubscriptionAsync);
        RemoveSubscriptionCommand = new AsyncRelayCommand(RemoveSubscriptionAsync);
        InitializeComponent();
        _initialTag = tag;
        Configure(simpleWorkType, privacyPolicy, viewModel);
        User = user;
    }

    public User? User
    {
        get => GetValue(UserProperty);
        set => SetValue(UserProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UserProperty)
            ApplyUser(change.GetNewValue<User?>());
        else if (change.Property == UiServices.ProviderProperty && _userPending)
            ApplyUser(User);
    }

    private bool EnsureServices()
    {
        _client ??= this.GetUiService<MakoClient>();
        _subscriptions ??= this.GetUiService<IWorkSubscriptionService>();
        _storage ??= this.GetUiService<StorageEngine>();
        _session ??= this.GetUiService<IUserSessionService>();
        _logger ??= this.GetUiService<FileLogger>();
        return _client is not null && _subscriptions is not null && _storage is not null && _session is not null && _logger is not null;
    }

    private void Configure(SimpleWorkType simpleWorkType, PrivacyPolicy privacyPolicy, IWorkViewViewModel? viewModel)
    {
        _suppressChangeSource = true;
        SimpleWorkTypeComboBox.SelectedValue = simpleWorkType;
        PrivacyPolicyComboBox.SelectedValue = privacyPolicy;
        _suppressChangeSource = false;
        _viewModel = viewModel;
    }

    private void ApplyUser(User? user)
    {
        if (_userPending = !EnsureServices())
            return;
        var next = user ?? EmptyUser();
        if (_initialized && next.Id == _user.Id)
            return;

        _user = next;
        UpdatePrivacyVisibility();
        _initialized = true;
        if (_viewModel is not null)
        {
            WorkContainer.SetViewModel(_viewModel);
            UpdateSubscriptionButtons();
            _viewModel = null;
            return;
        }

        if (_user.Id > 0)
            FetchTags();
        else
        {
            _suppressChangeSource = true;
            TagComboBox.ItemsSource = DefaultTags;
            TagComboBox.SelectedItem = AllBookmarkTag.Instance;
            _suppressChangeSource = false;
        }

        ChangeSource();
    }

    private void UpdatePrivacyVisibility()
    {
        var myId = _session!.CurrentUserId;
        var enabled = _user.Id > 0 && _user.Id == myId;
        PrivacyPolicyComboBox.IsEnabled = PrivacyPolicyComboBox.IsVisible = enabled;
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        if (!_initialized || !EnsureServices())
            return;
        FetchTags();
        ChangeSource();
    }

    private void TagComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppressChangeSource)
            return;

        ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        if (!EnsureServices())
            return;
        ChangeSource();
    }

    private void FetchTags() => _ = FetchTagsAsync();

    private async Task FetchTagsAsync()
    {
        if (_user.Id <= 0)
        {
            _suppressChangeSource = true;
            TagComboBox.ItemsSource = DefaultTags;
            TagComboBox.SelectedItem = AllBookmarkTag.Instance;
            _suppressChangeSource = false;
            return;
        }

        try
        {
            var tags = await _client!.GetBookmarkTagsAsync(
                _user.Id,
                SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
                PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>());

            _suppressChangeSource = true;
            TagComboBox.ItemsSource = tags;
            TagComboBox.SelectedItem = tags.Cast<BookmarkTag?>().FirstOrDefault(tag => tag!.Value.Name == _initialTag) ?? AllBookmarkTag.Instance;
            _suppressChangeSource = false;
        }
        catch (Exception ex)
        {
            _logger!.LogError(nameof(FetchTags), ex);
            _suppressChangeSource = true;
            TagComboBox.ItemsSource = new List<BookmarkTag> { AllBookmarkTag.Instance, UncategorizedBookmarkTag.Instance };
            TagComboBox.SelectedItem = AllBookmarkTag.Instance;
            _suppressChangeSource = false;
        }
    }

    private void ChangeSource()
    {
        if (_user.Id <= 0)
        {
            WorkContainer.ResetSource(AsyncEnumerable.Empty<object>(), SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>());
            UpdateSubscriptionButtons();
            return;
        }

        var tag = (TagComboBox.SelectedItem as IBookmarkTag)?.Name;
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
        var privacy = PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>() is PrivacyPolicy.Private ? "private" : "public";
        var workEntries = workType is SimpleWorkType.Novel
            ? _client!.NovelBookmarks(_user.Id, privacy, tag).AsWorkEntries()
            : _client!.WorkBookmarks(_user.Id, privacy, tag).AsWorkEntries();
        WorkContainer.ResetSource(workEntries, workType);
        _subscriptions!.QueueSyncCurrentSource(
            _user.Id,
            WorkSubscriptionType.Bookmarks,
            GetSubscriptionWorkKind(),
            workEntries);
        UpdateSubscriptionButtons();
    }

    private Task AddSubscriptionAsync()
    {
        var workKind = GetSubscriptionWorkKind();
        return WorkSubscriptionButtonHelper.RunAsync(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            () => Task.FromResult(WorkSubscriptionHelper.TryAddOrUpdateUser(
                _user,
                WorkSubscriptionType.Bookmarks,
                workKind,
                _storage!,
                _subscriptions!)),
            UpdateSubscriptionButtons);
    }

    private Task RemoveSubscriptionAsync() =>
        WorkSubscriptionButtonHelper.RunAsync(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            RemoveCurrentSubscriptionAsync,
            UpdateSubscriptionButtons);

    private void UpdateSubscriptionButtons()
    {
        if (_user.Id <= 0)
        {
            AddSubscriptionButton.IsVisible = false;
            RemoveSubscriptionButton.IsVisible = false;
            return;
        }

        WorkSubscriptionButtonHelper.UpdateVisibility(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            _subscriptions!.TryGetSubscription(_user.Id, WorkSubscriptionType.Bookmarks, GetSubscriptionWorkKind()) is not null);
    }

    private async Task<bool> RemoveCurrentSubscriptionAsync()
    {
        if (_subscriptions!.TryGetSubscription(_user.Id, WorkSubscriptionType.Bookmarks, GetSubscriptionWorkKind()) is not { HistoryEntryId: var historyEntryId })
            return false;

        _ = await _subscriptions!.TryRemoveAsync(historyEntryId);
        return true;
    }

    private WorkSubscriptionWorkKind GetSubscriptionWorkKind() =>
        SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>() is SimpleWorkType.Novel
            ? WorkSubscriptionWorkKind.Novel
            : WorkSubscriptionWorkKind.Illustration;
}
