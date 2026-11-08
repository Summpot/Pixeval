// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Mako;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class WorkRankingPage : IconContentPage
{
    private (SimpleWorkKind Type, RankMode Rank, DateTime Date)? _currentQuery;
    private readonly AppSettings _settings;
    private readonly MakoClient _client;
    private bool _changingOptions;

    public WorkRankingPage(AppSettings settings, MakoClient client)
        : this(settings.SearchSettings.DefaultSimpleWorkType, settings.SearchSettings.DefaultSimpleWorkType == SimpleWorkKind.Novel
            ? settings.SearchSettings.RankOptions.NovelRankOption : settings.SearchSettings.RankOptions.IllustrationRankOption, MaxDate, settings, client)
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
            Strings.WorkRankingPageInvalidDateTitle(),
            Strings.WorkRankingPageInvalidDateContent(MaxDate.ToString("d")));

    private void SimpleWorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        _changingOptions = true;
        try { ChangeEnumSource(); }
        finally { _changingOptions = false; }
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
        if (!_changingOptions && RankOptionComboBox.SelectedValue is not null)
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
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>();
        var rankOption = RankOptionComboBox.GetSelectedValue<RankMode>();
        var date = RankDateTimeCalendarDatePicker.SelectedDate ?? MaxDate;
        var query = (workType, rankOption, date.Date);
        if (!force && _currentQuery == query)
            return;
        _currentQuery = query;
        WorkContainer.ResetSource(_client.WorkRankingUnified(workType, rankOption, date.ToQueryDate()).AsObjects(), workType);

    }
}
