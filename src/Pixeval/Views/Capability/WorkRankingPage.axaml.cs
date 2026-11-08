// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Mako;
using Mako.Global.Enum;
using Pixeval.Controls;
using Pixeval.I18N;
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

    public static DateTime MaxDate => MakoClient.RankingMaxDateTime.LocalDateTime;

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
        var query = (SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
            RankOptionComboBox.GetSelectedValue<RankOption>(),
            (RankDateTimeCalendarDatePicker.SelectedDate ?? MaxDate).Date);
        if (!force && _currentQuery == query)
            return;
        _currentQuery = query;
        WorkContainer.ResetEngine(App.AppViewModel.MakoClient.WorkRanking(
            SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
            RankOptionComboBox.GetSelectedValue<RankOption>(),
            new(RankDateTimeCalendarDatePicker.SelectedDate ?? MaxDate)));
    }
}
