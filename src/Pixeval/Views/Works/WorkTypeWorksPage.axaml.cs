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
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Pixiv;
using Pixeval.Services.Background;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Works;

namespace Pixeval.Views.Works;

public abstract partial class WorkTypeWorksPage : IconContentPage
{
    private MakoClient? _client;
    private IWorkSubscriptionService? _subscriptions;
    private StorageEngine? _storage;
    private bool _sourcePending;

    protected WorkTypeWorksPage(MakoClient? client, IWorkSubscriptionService? subscriptions, StorageEngine? storage)
    {
        _client = client;
        _subscriptions = subscriptions;
        _storage = storage;
        InitializeComponent();
        AddSubscriptionCommand = new AsyncRelayCommand(AddSubscriptionAsync);
        RemoveSubscriptionCommand = new AsyncRelayCommand(RemoveSubscriptionAsync);
    }

    public IAsyncRelayCommand AddSubscriptionCommand { get; }

    public IAsyncRelayCommand RemoveSubscriptionCommand { get; }

    protected void InitializeSource(WorkType workType, IWorkViewViewModel? viewModel = null)
    {
        WorkTypeComboBox.SelectedValue = workType;

        if (viewModel is not null)
            WorkContainer.SetViewModel(viewModel);
        else
            ChangeSource();
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private bool EnsureServices()
    {
        _client ??= this.GetUiService<MakoClient>();
        _subscriptions ??= this.GetUiService<IWorkSubscriptionService>();
        _storage ??= this.GetUiService<StorageEngine>();
        return _client is not null && _subscriptions is not null && _storage is not null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UiServices.ProviderProperty && _sourcePending)
            ChangeSource();
    }

    protected void ChangeSource()
    {
        if (_sourcePending = !EnsureServices())
            return;
        var workType = WorkTypeComboBox.GetSelectedValue<WorkType>();
        var engine = GetFetchEngine(_client!, workType);
        WorkContainer.ResetEngine(engine);
        OnSourceChanged(engine, workType);
    }

    protected abstract IAsyncEnumerable<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType);

    protected virtual void OnSourceChanged(IAsyncEnumerable<IWorkEntry> engine, WorkType workType)
    {
    }

    protected IWorkSubscriptionService SubscriptionService =>
        EnsureServices() ? _subscriptions! : throw new InvalidOperationException("Services are not available before the page is attached to the shell.");

    protected StorageEngine Storage =>
        EnsureServices() ? _storage! : throw new InvalidOperationException("Services are not available before the page is attached to the shell.");

    protected void EnableAddSubscriptionButton() => AddSubscriptionButton.IsVisible = true;

    protected void UpdateSubscriptionButtons(
        long targetId,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind) =>
        WorkSubscriptionButtonHelper.UpdateVisibility(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            SubscriptionService.TryGetSubscription(targetId, subscriptionType, workKind) is not null);

    protected Task<TResult> RunSubscriptionOperationAsync<TResult>(
        Func<Task<TResult>> operation,
        Action updateButtons) =>
        WorkSubscriptionButtonHelper.RunAsync(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            operation,
            updateButtons);

    protected virtual Task AddSubscriptionAsync() => Task.CompletedTask;

    protected virtual Task RemoveSubscriptionAsync() => Task.CompletedTask;
}

public class WorkRecommendedPage : WorkTypeWorksPage
{
    public WorkRecommendedPage(AppSettings settings, MakoClient client, IWorkSubscriptionService subscriptions, StorageEngine storage)
        : this(settings.SearchSettings.WorkType, client, subscriptions, storage)
    {
    }

    public WorkRecommendedPage(
        WorkType workType,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage,
        IWorkViewViewModel? viewModel = null)
        : base(client, subscriptions, storage)
    {
        InitializeSource(workType, viewModel);
    }

    protected override IAsyncEnumerable<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType)
    {
        return workType switch
        {
            WorkType.Novel => makoClient.NovelRecommended(true, true).AsWorkEntries(),
            _ => makoClient.WorkRecommended(true, true).AsWorkEntries()
        };
    }
}

public class WorkNewPage : WorkTypeWorksPage
{
    public WorkNewPage(AppSettings settings, MakoClient client, IWorkSubscriptionService subscriptions, StorageEngine storage)
        : this(settings.SearchSettings.WorkType, client, subscriptions, storage)
    {
    }

    public WorkNewPage(
        WorkType workType,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage,
        IWorkViewViewModel? viewModel = null)
        : base(client, subscriptions, storage)
    {
        InitializeSource(workType, viewModel);
    }

    protected override IAsyncEnumerable<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType)
    {
        return workType is WorkType.Novel
            ? makoClient.NovelNew(null).AsWorkEntries()
            : makoClient.WorkNew(workType is WorkType.Manga ? "manga" : "illust", null).AsWorkEntries();
    }

}

public class WorkPostsPage : WorkTypeWorksPage
{
    public static readonly StyledProperty<User?> UserProperty =
        AvaloniaProperty.Register<WorkPostsPage, User?>(nameof(User));

    private static User EmptyUser() =>
        new(0, "", "", new ProfileImageUrls(null, null, null, null), false, null, []);

    private User _user = EmptyUser();
    private readonly WorkType _workType;
    private readonly IWorkViewViewModel? _viewModel;
    private bool _initialized;

    /// <summary>XAML constructor: services are resolved from <see cref="UiServices.ProviderProperty"/> once the page is attached under the shell.</summary>
    public WorkPostsPage() : base(null, null, null)
    {
        _workType = WorkType.Illustration;
    }

    public WorkPostsPage(User user, AppSettings settings, MakoClient client, IWorkSubscriptionService subscriptions, StorageEngine storage)
        : this(user, settings.SearchSettings.WorkType, client, subscriptions, storage)
    {
    }

    public WorkPostsPage(
        User user,
        WorkType workType,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage,
        IWorkViewViewModel? viewModel = null)
        : base(client, subscriptions, storage)
    {
        _workType = workType;
        _viewModel = viewModel;
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
    }

    private void ApplyUser(User? user)
    {
        var next = user ?? EmptyUser();
        if (_initialized && next.Id == _user.Id)
            return;

        _user = next;
        if (!_initialized)
        {
            _initialized = true;
            if (_user.Id > 0)
                EnableAddSubscriptionButton();
            InitializeSource(_workType, _viewModel);
            if (_viewModel is not null && _user.Id > 0)
                UpdateSubscriptionButtons(_user.Id, WorkSubscriptionType.Posts, GetSubscriptionWorkKind(_workType));
            return;
        }

        if (_user.Id > 0)
            EnableAddSubscriptionButton();
        ChangeSource();
    }

    protected override IAsyncEnumerable<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType)
    {
        if (_user.Id <= 0)
            return AsyncEnumerable.Empty<IWorkEntry>();

        return workType switch
        {
            WorkType.Novel => makoClient.NovelPosted(_user.Id).AsWorkEntries(),
            WorkType.Manga => makoClient.WorkPosted(_user.Id, "manga").AsWorkEntries(),
            _ => makoClient.WorkPosted(_user.Id, "illust").AsWorkEntries()
        };

    }

    protected override void OnSourceChanged(IAsyncEnumerable<IWorkEntry> engine, WorkType workType)
    {
        if (_user.Id <= 0)
            return;
        var workKind = GetSubscriptionWorkKind(workType);
        SubscriptionService.QueueSyncCurrentSource(
            _user.Id,
            WorkSubscriptionType.Posts,
            workKind,
            engine.ToFetchEngine());
        UpdateSubscriptionButtons(_user.Id, WorkSubscriptionType.Posts, workKind);
    }

    protected override async Task AddSubscriptionAsync()
    {
        var workType = WorkTypeComboBox.GetSelectedValue<WorkType>();
        var workKind = GetSubscriptionWorkKind(workType);

        _ = await RunSubscriptionOperationAsync(
            () => Task.FromResult(WorkSubscriptionHelper.TryAddOrUpdateUser(
                _user,
                WorkSubscriptionType.Posts,
                workKind,
                Storage,
                SubscriptionService)),
            () => UpdateSubscriptionButtons(WorkTypeComboBox.GetSelectedValue<WorkType>()));
    }

    protected override async Task RemoveSubscriptionAsync()
    {
        _ = await RunSubscriptionOperationAsync(
            RemoveCurrentSubscriptionAsync,
            () => UpdateSubscriptionButtons(WorkTypeComboBox.GetSelectedValue<WorkType>()));
    }

    private async Task<bool> RemoveCurrentSubscriptionAsync()
    {
        if (GetCurrentSubscriptionId() is not { } historyEntryId)
            return false;

        _ = await SubscriptionService.TryRemoveAsync(historyEntryId);
        return true;
    }

    private long? GetCurrentSubscriptionId() =>
        SubscriptionService.TryGetSubscription(
            _user.Id,
            WorkSubscriptionType.Posts,
            GetSubscriptionWorkKind(WorkTypeComboBox.GetSelectedValue<WorkType>()))?.HistoryEntryId;

    private void UpdateSubscriptionButtons(WorkType workType)
    {
        if (_user.Id <= 0)
        {
            AddSubscriptionButton.IsVisible = false;
            RemoveSubscriptionButton.IsVisible = false;
            return;
        }
        UpdateSubscriptionButtons(_user.Id, WorkSubscriptionType.Posts, GetSubscriptionWorkKind(workType));
    }

    private static WorkSubscriptionWorkKind GetSubscriptionWorkKind(WorkType workType) => workType switch
    {
        WorkType.Illustration => WorkSubscriptionWorkKind.Illustration,
        WorkType.Manga => WorkSubscriptionWorkKind.Manga,
        WorkType.Novel => WorkSubscriptionWorkKind.Novel,
        _ => throw new ArgumentOutOfRangeException(nameof(workType))
    };
}
