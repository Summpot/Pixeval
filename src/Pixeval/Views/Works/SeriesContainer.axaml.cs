// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Config;
using Pixeval.Pixiv;
using Pixeval.Services.Background;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Works;

namespace Pixeval.Views.Works;

public partial class SeriesContainer : UserControl
{
    private readonly MakoClient _client;
    private readonly IWorkSubscriptionService _subscriptions;
    private readonly StorageEngine _storage;
    private readonly SimpleWorkType _workType;
    private readonly long _seriesId;
    private readonly Series? _seriesDetail;
    private readonly IWorkEntry? _firstWork;

    public IAsyncRelayCommand AddSubscriptionCommand { get; }

    public IAsyncRelayCommand RemoveSubscriptionCommand { get; }

    public SeriesContainer(
        SimpleWorkType workType,
        long seriesId,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage)
        : this(workType, seriesId, null, null, client, subscriptions, storage)
    {
        ChangeSource();
    }

    public SeriesContainer(
        SimpleWorkType workType,
        long seriesId,
        IWorkViewViewModel viewModel,
        Series seriesDetail,
        IWorkEntry firstWork,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage)
        : this(workType, seriesId, seriesDetail, firstWork, client, subscriptions, storage)
    {
        WorkContainer.IsRefreshEnabled = false;
        WorkContainer.SetViewModel(viewModel);
        UpdateSubscriptionButtons();
    }

    public SeriesContainer(
        SimpleWorkType workType,
        long seriesId,
        IFetchEngine<IWorkEntry> engine,
        Series seriesDetail,
        IWorkEntry firstWork,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage)
        : this(workType, seriesId, seriesDetail, firstWork, client, subscriptions, storage)
    {
        WorkContainer.IsRefreshEnabled = false;
        SetEngine(engine);
    }

    private SeriesContainer(
        SimpleWorkType workType,
        long seriesId,
        Series? seriesDetail,
        IWorkEntry? firstWork,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage)
    {
        _workType = workType;
        _seriesId = seriesId;
        _seriesDetail = seriesDetail;
        _firstWork = firstWork;
        _client = client;
        _subscriptions = subscriptions;
        _storage = storage;
        AddSubscriptionCommand = new AsyncRelayCommand(AddSubscriptionAsync);
        RemoveSubscriptionCommand = new AsyncRelayCommand(RemoveSubscriptionAsync);
        InitializeComponent();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e) => ChangeSource();

    private void ChangeSource() => SetEngine(_client.WorkSeries(_workType, _seriesId));

    private void SetEngine(IFetchEngine<IWorkEntry> engine)
    {
        WorkContainer.ResetEngine(engine);
        _subscriptions.QueueSyncCurrentSource(
            _seriesId,
            WorkSubscriptionType.Series,
            GetSubscriptionWorkKind(),
            engine);
        UpdateSubscriptionButtons();
    }

    private Task AddSubscriptionAsync()
    {
        var workKind = GetSubscriptionWorkKind();
        return WorkSubscriptionButtonHelper.RunAsync(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            () => Task.FromResult(WorkSubscriptionHelper.TryAddOrUpdateSeries(
                _seriesId,
                workKind,
                _storage,
                _subscriptions,
                _seriesDetail,
                _firstWork)),
            UpdateSubscriptionButtons);
    }

    private Task RemoveSubscriptionAsync() =>
        WorkSubscriptionButtonHelper.RunAsync(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            RemoveCurrentSubscriptionAsync,
            UpdateSubscriptionButtons);

    private void UpdateSubscriptionButtons() =>
        WorkSubscriptionButtonHelper.UpdateVisibility(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            _subscriptions.TryGetSubscription(_seriesId, WorkSubscriptionType.Series, GetSubscriptionWorkKind()) is not null);

    private async Task<bool> RemoveCurrentSubscriptionAsync()
    {
        if (_subscriptions.TryGetSubscription(_seriesId, WorkSubscriptionType.Series, GetSubscriptionWorkKind()) is not { HistoryEntryId: var historyEntryId })
            return false;

        _ = await _subscriptions.TryRemoveAsync(historyEntryId);
        return true;
    }

    private WorkSubscriptionWorkKind GetSubscriptionWorkKind() =>
        _workType is SimpleWorkType.Novel
            ? WorkSubscriptionWorkKind.Novel
            : WorkSubscriptionWorkKind.Manga;
}
