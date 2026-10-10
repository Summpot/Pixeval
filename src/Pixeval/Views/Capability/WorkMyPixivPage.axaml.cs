// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement.Settings;
using Pixeval.Controls;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Capability;

public partial class WorkMyPixivPage : IconContentPage
{
    public WorkMyPixivPage() : this(App.Services!.GetRequiredService<AppSettings>().SearchSettings.DefaultSimpleWorkType)
    {
    }

    public WorkMyPixivPage(SimpleWorkType simpleWorkType, IWorkViewViewModel? viewModel = null)
    {
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
        var engine = App.Services!.GetRequiredService<MakoClient>().WorkMyPixiv(workType);
        WorkContainer.ResetEngine(engine);
    }
}
