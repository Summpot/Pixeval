// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.ComponentModel;
using Avalonia.Interactivity;
using Pixeval.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Works;

namespace Pixeval.Views.Viewers;

public partial class SeriesViewerPage : IconContentPage
{
    private readonly MakoClient _client;
    private readonly IWorkSubscriptionService _subscriptions;
    private readonly StorageEngine _storage;

    public SeriesViewerPage(MakoClient client, IWorkSubscriptionService subscriptions, StorageEngine storage)
        : this(null, client, subscriptions, storage)
    {
    }

    public SeriesViewerPage(
        SeriesViewerPageViewModel? viewModel,
        MakoClient client,
        IWorkSubscriptionService subscriptions,
        StorageEngine storage)
    {
        _client = client;
        _subscriptions = subscriptions;
        _storage = storage;
        DataContext = viewModel;
        InitializeComponent();
        if (viewModel is null)
            return;

        viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        if (viewModel is { SeriesDetail: { } seriesDetail, FirstWork: { } firstWork }
            && viewModel.TakeWorksViewModel() is { } worksViewModel)
        {
            SeriesContainerHost.Content = new SeriesContainer(
                viewModel.WorkType,
                viewModel.Id,
                worksViewModel,
                seriesDetail,
                firstWork,
                _client,
                _subscriptions,
                _storage);
        }
        else
        {
            SetWorksEngine(viewModel, viewModel.WorksEngine);
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SeriesViewerPageViewModel.WorksEngine)
            && sender is SeriesViewerPageViewModel viewModel)
            SetWorksEngine(viewModel, viewModel.WorksEngine);
    }

    private void SetWorksEngine(
        SeriesViewerPageViewModel viewModel,
        IFetchEngine<IWorkEntry>? engine)
    {
        SeriesContainerHost.Content = engine is not null
                                      && viewModel is { SeriesDetail: { } seriesDetail, FirstWork: { } firstWork }
            ? new SeriesContainer(viewModel.WorkType, viewModel.Id, engine, seriesDetail, firstWork, _client, _subscriptions, _storage)
            : null;
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is SeriesViewerPageViewModel viewModel)
            RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, viewModel));
    }
}
