// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Works;

namespace Pixeval.Views.Viewers;

public partial class SeriesViewerPage : IconContentPage, IDisposable
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
                viewModel.WorkKind,
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
        IAsyncEnumerable<WorkEntry>? engine)
    {
        DisposeWorksContent();
        SeriesContainerHost.Content = engine is not null
                                      && viewModel is { SeriesDetail: { } seriesDetail, FirstWork: { } firstWork }
            ? new SeriesContainer(viewModel.WorkKind, viewModel.Id, engine, seriesDetail, firstWork, _client, _subscriptions, _storage)
            : null;
    }

    private void ProfileGrid_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // The tab shell also consumes horizontal space. Stack the profile at the
        // available content width rather than assuming the page fills the window.
        var narrow = e.NewSize.Width < 280;
        Grid.SetRow(ProfileText, narrow ? 1 : 0);
        Grid.SetColumn(ProfileText, narrow ? 0 : 1);
        Grid.SetColumnSpan(ProfileText, narrow ? 2 : 1);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, this));
    }

    private void DisposeWorksContent()
    {
        if (SeriesContainerHost.Content is Control content)
            foreach (var view in content.GetLogicalDescendants().OfType<WorkView>())
                view.Dispose();
        SeriesContainerHost.Content = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        if (_viewModel is { } model)
        {
            model.PropertyChanged -= ViewModel_OnPropertyChanged;
            model.Dispose();
        }
        _viewModel = null;
        DisposeWorksContent();
        DataContext = null;
    }
}
