// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement.Settings;
using Pixeval.Controls;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Work;

namespace Pixeval.Views.Capability;

public abstract partial class WorkTypeWorksPage : IconContentPage
{
    protected WorkTypeWorksPage() => InitializeComponent();

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

    protected void ChangeSource()
    {
        var workType = WorkTypeComboBox.GetSelectedValue<WorkType>();
        var engine = GetFetchEngine(App.Services!.GetRequiredService<MakoClient>(), workType);
        WorkContainer.ResetEngine(engine);
        OnSourceChanged(engine, workType);
    }

    protected abstract IAsyncEnumerable<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType);

    protected virtual void OnSourceChanged(IAsyncEnumerable<IWorkEntry> engine, WorkType workType)
    {
    }

    protected static IWorkSubscriptionService SubscriptionService =>
        App.Services!.GetRequiredService<IWorkSubscriptionService>();

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

    private async void AddSubscriptionButton_OnClicked(object? sender, RoutedEventArgs e) => await AddSubscriptionAsync();

    private async void RemoveSubscriptionButton_OnClicked(object? sender, RoutedEventArgs e) => await RemoveSubscriptionAsync();

    protected virtual Task AddSubscriptionAsync() => Task.CompletedTask;

    protected virtual Task RemoveSubscriptionAsync() => Task.CompletedTask;
}

public class WorkRecommendedPage : WorkTypeWorksPage
{
    public WorkRecommendedPage() : this(App.Services!.GetRequiredService<AppSettings>().SearchSettings.WorkType)
    {
    }

    public WorkRecommendedPage(WorkType workType, IWorkViewViewModel? viewModel = null)
    {
        InitializeSource(workType, viewModel);
    }

    protected override IAsyncEnumerable<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType)
    {
        return workType switch
        {
            WorkType.Novel => makoClient.NovelRecommended(true, true),
            _ => makoClient.WorkRecommended(true, true)
        };
    }
}

public class WorkNewPage : WorkTypeWorksPage
{
    public WorkNewPage() : this(App.Services!.GetRequiredService<AppSettings>().SearchSettings.WorkType)
    {
    }

    public WorkNewPage(WorkType workType, IWorkViewViewModel? viewModel = null)
    {
        InitializeSource(workType, viewModel);
    }

    protected override IAsyncEnumerable<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType)
    {
        return workType is WorkType.Novel
            ? makoClient.NovelNew(null)
            : makoClient.WorkNew(workType is WorkType.Manga ? "manga" : "illust", null);
    }
}

public class WorkPostsPage : WorkTypeWorksPage
{
    public static readonly StyledProperty<User?> UserProperty =
        AvaloniaProperty.Register<WorkPostsPage, User?>(nameof(User));

    private static User EmptyUser() =>
        new(0, "", "", new ProfileImageUrls(null, null, null, null), false, null, []);

    private User _user = EmptyUser();
    private WorkType _workType = App.Services!.GetRequiredService<AppSettings>().SearchSettings.WorkType;
    private IWorkViewViewModel? _viewModel;
    private bool _initialized;

    public WorkPostsPage()
    {
    }

    public WorkPostsPage(User user) : this(user, App.Services!.GetRequiredService<AppSettings>().SearchSettings.WorkType)
    {
    }

    public WorkPostsPage(User user, WorkType workType, IWorkViewViewModel? viewModel = null)
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
            WorkType.Novel => makoClient.NovelPosted(_user.Id),
            WorkType.Manga => makoClient.WorkPosted(_user.Id, "manga"),
            _ => makoClient.WorkPosted(_user.Id, "illust")
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
                App.Services!.GetRequiredService<StorageEngine>(),
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
