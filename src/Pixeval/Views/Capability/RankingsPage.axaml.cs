using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Mako;
using Mako.Global.Enum;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Pixeval.Utilities;

namespace Pixeval;

public partial class RankingsPage : UserControl
{
    private bool _suppressChangeSource;
    private bool _suppressModeSelectionChanged;

    public RankingsPage()
    {
        InitializeComponent();
        AddHandler(Frame.NavigatedToEvent, (sender, e) =>
        {
            SyncFromCurrentWorkType();
            ChangeEnumSource();
            ChangeSource();
        });

        AttachedToVisualTree += (_, _) => App.AppViewModel.CurrentWorkTypeChanged += AppViewModelOnCurrentWorkTypeChanged;
        DetachedFromVisualTree += (_, _) => App.AppViewModel.CurrentWorkTypeChanged -= AppViewModelOnCurrentWorkTypeChanged;
    }

    public static DateTime MaxDate => MakoClient.RankingMaxDate.LocalDateTime;

    private void SimpleWorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        var selected = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
        App.AppViewModel.SetCurrentWorkType(selected.ToWorkType(App.AppViewModel.CurrentWorkType));

        if (_suppressModeSelectionChanged)
            return;

        ChangeEnumSource();
        ChangeSource();
    }

    private void ChangeEnumSource()
    {
        _suppressChangeSource = true;
        if (SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>() is SimpleWorkType.IllustrationAndManga)
        {
            RankOptionComboBox.ItemsSource = RankOptionExtension.IllustrationItems;
            RankOptionComboBox.SelectedIndex = (int) App.AppViewModel.AppSettings.IllustrationRankOption;
        }
        else
        {
            RankOptionComboBox.ItemsSource = RankOptionExtension.NovelItems;
            RankOptionComboBox.SelectedIndex = (int) App.AppViewModel.AppSettings.NovelRankOption;
        }
        _suppressChangeSource = false;
    }

    private void RankOptionComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        if (!_suppressChangeSource && RankOptionComboBox.SelectedValue is not null)
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
        // 傻逼bug see also https://github.com/AvaloniaUI/Avalonia/issues/18418
        // ChangeSource();
    }

    private void ChangeSource()
    {
        WorkContainer.ResetEngine(App.AppViewModel.MakoClient.WorkRanking(
            SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
            RankOptionComboBox.GetSelectedValue<RankOption>(),
            new(RankDateTimeCalendarDatePicker.SelectedDate ?? MaxDate)));
    }

    private void AppViewModelOnCurrentWorkTypeChanged(object? sender, WorkType workType)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SyncFromCurrentWorkType();
            ChangeEnumSource();
            ChangeSource();
        });
    }

    private void SyncFromCurrentWorkType()
    {
        _suppressModeSelectionChanged = true;
        SimpleWorkTypeComboBox.SelectedIndex = (int) App.AppViewModel.CurrentWorkType.ToSimpleWorkType();
        _suppressModeSelectionChanged = false;
    }
}
