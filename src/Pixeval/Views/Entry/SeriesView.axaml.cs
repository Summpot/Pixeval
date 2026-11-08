// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Entry;

public partial class SeriesView : UserControl
{
    public SeriesView() => InitializeComponent();

    public void SetViewModel(SeriesViewViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        var oldViewModel = DataContext as IDisposable;
        DataContext = viewModel;
        oldViewModel?.Dispose();
        if (IsLoaded)
            RegisterViewModelDisposal(viewModel);
    }

    private void SeriesItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not SeriesItem { DataContext: SeriesItemViewModel viewModel } || !ReferenceEquals(e.Source, sender))
            return;

        e.Handled = true;
        TopLevel.GetTopLevel(this)?.ViewContainer?.CreateSeriesPage(viewModel.WorkType, viewModel.Entry.Id);
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is SeriesViewViewModel viewModel)
            RegisterViewModelDisposal(viewModel);
    }

    private void RegisterViewModelDisposal(SeriesViewViewModel viewModel) =>
        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, viewModel));
}
