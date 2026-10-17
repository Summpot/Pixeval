// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Mako;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class WorkRankingPage : IconContentPage
{
    private readonly AppSettings _settings;
    private readonly MakoClient _client;

    public WorkRankingPage(AppSettings settings, MakoClient client)
        : this(settings.SearchSettings.DefaultSimpleWorkType, settings.SearchSettings.RankOptions.IllustrationRankOption, MaxDate, settings, client)
    {
    }

    public WorkRankingPage(
        SimpleWorkKind simpleWorkType,
        RankMode rankOption,
        DateTime rankingDate,
        AppSettings settings,
        MakoClient client,
        IWorkViewViewModel? viewModel = null)
    {
        _settings = settings;
        _client = client;
        InitializeComponent();
        SimpleWorkTypeComboBox.SelectedValue = simpleWorkType;
        ChangeEnumSource();
        RankOptionComboBox.SelectedValue = rankOption;
        RankDateTimeCalendarDatePicker.SelectedDate = rankingDate;
        if (viewModel is not null)
            WorkContainer.SetViewModel(viewModel);
        else
            ChangeSource();
    }

    public static DateTime MaxDate => MakoClient.RankingMaxDateTime.LocalDateTime;

    private void SimpleWorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        var oldRankOption = RankOptionComboBox.SelectedValue;
        ChangeEnumSource();
        if (Equals(oldRankOption, RankOptionComboBox.SelectedValue))
            ChangeSource();
    }

    private void ChangeEnumSource()
    {
        var selectedWorkType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>();
        RankOptionComboBox.ItemsSource = SymbolComboBoxItem.GetValues<RankMode>(selectedWorkType);
        RankOptionComboBox.SelectedValue = selectedWorkType is SimpleWorkKind.Illustration
            ? _settings.SearchSettings.RankOptions.IllustrationRankOption
            : _settings.SearchSettings.RankOptions.NovelRankOption;
    }

    private void RankOptionComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        if (RankOptionComboBox.SelectedValue is not null)
            ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void CalendarDatePicker_OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        ChangeSource();
    }

    private void ChangeSource()
    {
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>();
        var rankOption = RankOptionComboBox.GetSelectedValue<RankMode>();
        var date = RankDateTimeCalendarDatePicker.SelectedDate ?? MaxDate;
        WorkContainer.ResetSource(_client.WorkRankingUnified(workType, rankOption, date.ToQueryDate()).AsObjects(), workType);

    }
}
