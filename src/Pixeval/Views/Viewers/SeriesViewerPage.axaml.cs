// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Entry;
using Pixeval.Views.Work;

namespace Pixeval.Views.Viewers;

public partial class SeriesViewerPage : IconContentPage, IDisposable
{
    private SeriesViewerPageViewModel? _viewModel;
    private bool _isDisposed;

    public SeriesViewerPage() : this(null)
    {
    }

    public SeriesViewerPage(SeriesViewerPageViewModel? viewModel)
    {
        DataContext = _viewModel = viewModel;
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
                firstWork);
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
        DisposeWorksContent();
        SeriesContainerHost.Content = engine is not null
                                      && viewModel is { SeriesDetail: { } seriesDetail, FirstWork: { } firstWork }
            ? new SeriesContainer(viewModel.WorkType, viewModel.Id, engine, seriesDetail, firstWork)
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
