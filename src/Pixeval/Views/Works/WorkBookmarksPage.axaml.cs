// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private CancellationTokenSource? _tagLoadCancellation;

    public static IReadOnlyList<BookmarkTag> DefaultTags { get; } = [AllBookmarkTag.Instance, UncategorizedBookmarkTag.Instance];

    public IAsyncRelayCommand AddSubscriptionCommand { get; }

    public IAsyncRelayCommand RemoveSubscriptionCommand { get; }

    /// <summary>XAML constructor: services are resolved from <see cref="UiServices.ProviderProperty"/> once the page is attached under the shell.</summary>
    public WorkBookmarksPage()
    {
        AddSubscriptionCommand = new AsyncRelayCommand(AddSubscriptionAsync);
        RemoveSubscriptionCommand = new AsyncRelayCommand(RemoveSubscriptionAsync);
        InitializeComponent();
        Configure(SimpleWorkKind.Illustration, PrivacyRestrict.Public, null);
    }

    public WorkBookmarksPage(
        User user,
        AppSettings settings,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage,
        IUserSessionService session,
        FileLogger logger)
        : this(user, settings.SearchSettings.DefaultSimpleWorkType, PrivacyRestrict.Public, null, client, subscriptions, storage, session, logger)
    {
    }

    public WorkBookmarksPage(
        User user,
        SimpleWorkKind simpleWorkType,
        PrivacyRestrict privacyPolicy,
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
        var selected = DefaultTags.FirstOrDefault(item => item.Name == tag);
        if (tag is not null && selected.Name != tag)
            selected = new BookmarkTag(tag, 0, false);
        TagComboBox.ItemsSource = tag is null ? DefaultTags : [.. DefaultTags.Where(item => item.Name != tag), selected];
        TagComboBox.SelectedItem = selected;
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

    private void Configure(SimpleWorkKind simpleWorkType, PrivacyRestrict privacyPolicy, IWorkViewViewModel? viewModel)
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
        FetchTags();
        ChangeSource();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (EnsureServices()) FetchTags();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        CancelTagLoad();
        base.OnUnloaded(e);
    }

    private void CancelTagLoad()
    {
        var cancellation = _tagLoadCancellation;
        _tagLoadCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private void FetchTags() => _ = FetchTagsAsync();

    private async Task FetchTagsAsync()
    {
        if (!IsLoaded) return;
        CancelTagLoad();
        if (_user.Id <= 0)
        {
            _suppressChangeSource = true;
            TagComboBox.ItemsSource = DefaultTags;
            TagComboBox.SelectedItem = AllBookmarkTag.Instance;
            _suppressChangeSource = false;
            return;
        }

        var cancellation = _tagLoadCancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        try
        {
            var tags = await _client!.GetBookmarkTagsAsync(
                _user.Id,
                SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>(),
                PrivacyPolicyComboBox.GetSelectedValue<PrivacyRestrict>(), token);

            if (!IsLoaded || token.IsCancellationRequested || !ReferenceEquals(_tagLoadCancellation, cancellation))
                return;
            var selected = TagComboBox.SelectedItem is BookmarkTag tag ? tag : AllBookmarkTag.Instance;
            if (!tags.Any(item => item.Name == selected.Name))
                tags.Add(new BookmarkTag(selected.Name, 0, false));

            _suppressChangeSource = true;
            TagComboBox.ItemsSource = tags;
            TagComboBox.SelectedItem = tags.First(item => item.Name == selected.Name);
            _suppressChangeSource = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (IsLoaded && !token.IsCancellationRequested && ReferenceEquals(_tagLoadCancellation, cancellation))
            {
                _logger!.LogError(nameof(FetchTags), ex);
                this.Notifications.ShowError(Strings.EntryViewerPageLoadFailed(), ex.Message);
            }
        }
        finally
        {
            if (ReferenceEquals(_tagLoadCancellation, cancellation))
                _tagLoadCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ChangeSource()
    {
        if (_user.Id <= 0)
        {
            WorkContainer.ResetSource(AsyncEnumerable.Empty<object>(), SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>());
            UpdateSubscriptionButtons();
            return;
        }

        var tag = (TagComboBox.SelectedItem as IBookmarkTag)?.Name;
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>();
        var privacy = PrivacyPolicyComboBox.GetSelectedValue<PrivacyRestrict>();
        var workEntries = _client!.WorkBookmarksUnified(workType, _user.Id, privacy, tag);
        WorkContainer.ResetSource(workEntries.AsObjects(), workType);
        if (_subscriptions!.TryGetSubscription(_user.Id, WorkSubscriptionType.Bookmarks, GetSubscriptionWorkKind()) is { } subscription)
            _subscriptions!.QueueSyncSubscription(subscription);
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
        SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>() is SimpleWorkKind.Novel
            ? WorkSubscriptionWorkKind.Novel
            : WorkSubscriptionWorkKind.Illustration;
}
