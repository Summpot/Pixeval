// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Interactivity;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class WorkMyPixivPage : IconContentPage
{
    private readonly MakoClient _client;

    public WorkMyPixivPage(AppSettings settings, MakoClient client)
        : this(settings.SearchSettings.DefaultSimpleWorkType, client)
    {
    }

    public WorkMyPixivPage(SimpleWorkType simpleWorkType, MakoClient client, IWorkViewViewModel? viewModel = null)
    {
        _client = client;
        InitializeComponent();
        SimpleWorkTypeComboBox.SelectedValue = simpleWorkType;
        if (viewModel is not null)
            WorkContainer.SetViewModel(viewModel);
        else
            ChangeSource();
    }

    private void SimpleWorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void ChangeSource()
    {
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
        var engine = _client.WorkMyPixiv(workType);
        WorkContainer.ResetEngine(engine);
    }
}
