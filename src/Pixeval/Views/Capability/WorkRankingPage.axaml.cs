// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Pixeval.Controls;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Capability;

public partial class WorkRankingPage : IconContentPage
{
    private (SimpleWorkType Type, RankOption Rank, DateTime Date)? _currentQuery;
    public WorkRankingPage() : this(PixevalSettings.SimpleWorkType, GetDefaultRankOption(PixevalSettings.SimpleWorkType), MaxDate)
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
        {
            _currentQuery = (simpleWorkType, rankOption, rankingDate.Date);
            WorkContainer.SetViewModel(viewModel);
        }
        else
            ChangeSource();
    }

    public static DateTime MaxDate => MakoHelper.RankingMaxDateTime.LocalDateTime;

    private void RankDateTimeCalendarDatePicker_OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        // DisplayDateEnd limits the calendar viewport; Avalonia can extend it when
        // parsing typed text. Blackout dates enforce the API's date limit as well.
        if (RankDateTimeCalendarDatePicker.BlackoutDates is { } dates)
        {
            dates.Clear();
            dates.Add(new(MaxDate.Date.AddDays(1), DateTime.MaxValue.Date));
        }
    }

    private void RankDateTimeCalendarDatePicker_OnDateValidationError(object? sender, CalendarDatePickerDateValidationErrorEventArgs e) =>
        TopLevel.GetTopLevel(this)?.ViewContainer?.ShowWarning(
            I18NManager.GetResource(WorkRankingPageResources.InvalidDate.Title),
            I18NManager.GetResource(WorkRankingPageResources.InvalidDate.Content, MaxDate.ToString("d")));

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
        RankOptionComboBox.SelectedValue = GetDefaultRankOption(selectedWorkType);
    }

    private static RankOption GetDefaultRankOption(SimpleWorkType workType) =>
        workType is SimpleWorkType.Illustration
            ? App.AppViewModel.AppSettings.SearchSettings.RankOptions.IllustrationRankOption
            : App.AppViewModel.AppSettings.SearchSettings.RankOptions.NovelRankOption;

    private void RankOptionComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        if (RankOptionComboBox.SelectedValue is not null)
            ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource(force: true);
    }

    private void CalendarDatePicker_OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        ChangeSource();
    }

    private void ChangeSource(bool force = false)
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
