// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Controls;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Capability;

public partial class WorkRankingPage : IconContentPage
{
    public WorkRankingPage() : this(PixevalSettings.SimpleWorkType, App.AppViewModel.AppSettings.SearchSettings.RankOptions.IllustrationRankOption, MaxDate)
    {
    }

    public WorkRankingPage(SimpleWorkType simpleWorkType, RankOption rankOption, DateTime rankingDate, IWorkViewViewModel? viewModel = null)
    {
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

    public static DateTime MaxDate => MakoHelper.RankingMaxDateTime.LocalDateTime;

    private void SimpleWorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        var oldRankOption = RankOptionComboBox.SelectedValue;
        ChangeEnumSource();
        if (Equals(oldRankOption, RankOptionComboBox.SelectedValue))
            ChangeSource();
    }

    private void ChangeEnumSource()
    {
        var selectedWorkType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
        RankOptionComboBox.ItemsSource = SymbolComboBoxItem.GetValues<RankOption>(selectedWorkType);
        RankOptionComboBox.SelectedValue = selectedWorkType is SimpleWorkType.Illustration
            ? App.AppViewModel.AppSettings.SearchSettings.RankOptions.IllustrationRankOption
            : App.AppViewModel.AppSettings.SearchSettings.RankOptions.NovelRankOption;
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
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
        var mode = RankOptionComboBox.GetSelectedValue<RankOption>().ToString().ToLowerInvariant();
        var date = (RankDateTimeCalendarDatePicker.SelectedDate ?? MaxDate).ToString("yyyy-MM-dd");
        if (workType is SimpleWorkType.Novel)
        {
            WorkContainer.ResetEngine(App.AppViewModel.MakoClient.NovelRanking(mode, date));
        }
        else
        {
            WorkContainer.ResetEngine(App.AppViewModel.MakoClient.WorkRanking(mode, date));
        }
    }
}
