// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Models.Subscriptions;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Work;

namespace Pixeval.Views.Entry;

public partial class SeriesContainer : UserControl
{
    private readonly SimpleWorkType _workType;
    private readonly long _seriesId;
    private readonly Series? _seriesDetail;
    private readonly IWorkEntry? _firstWork;

    private static IWorkSubscriptionService SubscriptionService =>
        App.Services!.GetRequiredService<IWorkSubscriptionService>();

    public SeriesContainer()
    {
        InitializeComponent();
    }

    public SeriesContainer(SimpleWorkType workType, long seriesId)
        : this(workType, seriesId, null, null)
    {
        ChangeSource();
    }

    public SeriesContainer(
        SimpleWorkType workType,
        long seriesId,
        IWorkViewViewModel viewModel,
        Series seriesDetail,
        IWorkEntry firstWork)
        : this(workType, seriesId, seriesDetail, firstWork)
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
        IWorkEntry firstWork)
        : this(workType, seriesId, seriesDetail, firstWork)
    {
        WorkContainer.IsRefreshEnabled = false;
        SetEngine(engine);
    }

    private SeriesContainer(
        SimpleWorkType workType,
        long seriesId,
        Series? seriesDetail,
        IWorkEntry? firstWork)
    {
        _workType = workType;
        _seriesId = seriesId;
        _seriesDetail = seriesDetail;
        _firstWork = firstWork;
        InitializeComponent();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e) => ChangeSource();

    private void ChangeSource() => SetEngine(App.Services!.GetRequiredService<MakoClient>().WorkSeries(_workType, _seriesId));

    private void SetEngine(IFetchEngine<IWorkEntry> engine)
    {
        WorkContainer.ResetEngine(engine);
        SubscriptionService.QueueSyncCurrentSource(
            _seriesId,
            WorkSubscriptionType.Series,
            GetSubscriptionWorkKind(),
            engine);
        UpdateSubscriptionButtons();
    }

    private async void AddSubscriptionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        var workKind = GetSubscriptionWorkKind();
        _ = await WorkSubscriptionButtonHelper.RunAsync(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            () => Task.FromResult(WorkSubscriptionHelper.TryAddOrUpdateSeries(
                _seriesId,
                workKind,
                App.Services!.GetRequiredService<StorageEngine>(),
                SubscriptionService,
                _seriesDetail,
                _firstWork)),
            UpdateSubscriptionButtons);
    }

    private async void RemoveSubscriptionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        _ = await WorkSubscriptionButtonHelper.RunAsync(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            RemoveCurrentSubscriptionAsync,
            UpdateSubscriptionButtons);
    }

    private void UpdateSubscriptionButtons() =>
        WorkSubscriptionButtonHelper.UpdateVisibility(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            SubscriptionService.TryGetSubscription(_seriesId, WorkSubscriptionType.Series, GetSubscriptionWorkKind()) is not null);

    private async Task<bool> RemoveCurrentSubscriptionAsync()
    {
        if (SubscriptionService.TryGetSubscription(_seriesId, WorkSubscriptionType.Series, GetSubscriptionWorkKind()) is not { HistoryEntryId: var historyEntryId })
            return false;

        _ = await SubscriptionService.TryRemoveAsync(historyEntryId);
        return true;
    }

    private WorkSubscriptionWorkKind GetSubscriptionWorkKind() =>
        _workType is SimpleWorkType.Novel
            ? WorkSubscriptionWorkKind.Novel
            : WorkSubscriptionWorkKind.Manga;
}
