// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Interactivity;
using Pixeval.Controls;
using Pixeval.Pixiv;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class SeriesPage : IconContentPage
{
    private readonly MakoClient _client;
    private bool _initialized;

    public SeriesPage(MakoClient client, Pixeval.Config.AppSettings settings)
    {
        _client = client;
        InitializeComponent();
        SimpleWorkTypeComboBox.SelectedValue = settings.SearchSettings.DefaultSimpleWorkType;
        _initialized = true;
        ChangeSource();
    }

    private void SimpleWorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        ChangeSource();
    }

    private void RefreshButton_OnClick(object? sender, RoutedEventArgs e) => ChangeSource();

    private void ChangeSource()
    {
        if (!_initialized) return;
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>();
        (SeriesView.DataContext as SeriesViewViewModel)?.ResetSource(
            _client.WorkSeriesWatchlistUnified(workType),
            workType);
    }
}
