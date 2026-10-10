// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.Models.Filters;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Filters;
using Pixeval.ViewModels;

namespace Pixeval.Views.Work;

/// <summary>
/// 所有插画集合通用的容器
/// </summary>
public partial class WorkContainer : UserControl
{
    private readonly WorkContainerFilterCoordinator _filterCoordinator = new();

    public static readonly DirectProperty<WorkContainer, bool> IsRefreshEnabledProperty = AvaloniaProperty.RegisterDirect<WorkContainer, bool>(
        nameof(IsRefreshEnabled),
        o => o.IsRefreshEnabled,
        (o, v) => o.IsRefreshEnabled = v);

    public static readonly DirectProperty<WorkContainer, bool> IsCommandBarCollapsedProperty = AvaloniaProperty.RegisterDirect<WorkContainer, bool>(
        nameof(IsCommandBarCollapsed),
        o => o.IsCommandBarCollapsed,
        (o, v) => o.IsCommandBarCollapsed = v);

    public bool IsCommandBarCollapsed
    {
        get;
        set => SetAndRaise(IsCommandBarCollapsedProperty, ref field, value);
    }

    public bool IsRefreshEnabled
    {
        get;
        set
        {
            if (field == value)
                return;

            _ = SetAndRaise(IsRefreshEnabledProperty, ref field, value);
            UpdateRefreshButton();
            return;

            void UpdateRefreshButton()
            {
                if (IsRefreshEnabled)
                {
                    if (!RightToolBar.SecondaryCommands.Contains(RefreshButton))
                        RightToolBar.SecondaryCommands.Insert(0, RefreshButton);

                    return;
                }

                _ = RightToolBar.SecondaryCommands.Remove(RefreshButton);
            }
        }
    } = true;

    public event EventHandler<RoutedEventArgs>? RefreshRequested;

    public AvaloniaList<Control> CommandBarElements { get; } = [];

    public AvaloniaList<ICommandBarElement> CommandBarSubElements { get; } = [];

    public WorkContainer()
    {
        InitializeComponent();

        CommandBarElements.CollectionChanged += (_, e) =>
        {
            if (e is { Action: NotifyCollectionChangedAction.Add, NewItems: { } newItems })
                foreach (Control argsNewItem in newItems)
                    ExtraCommandsBar.Children.Insert(0, argsNewItem);
            else
                throw new ArgumentException("This collection does not support operations except the Add");
        };

        CommandBarSubElements.CollectionChanged += (_, e) =>
        {
            if (e is { Action: NotifyCollectionChangedAction.Add, NewItems: { } newItems })
                foreach (ICommandBarElement argsNewItem in newItems)
                    RightToolBar.SecondaryCommands.Add(argsNewItem);
            else
                throw new ArgumentException("This collection does not support operations except the Add");
        };
    }

    private void SelectAllToggleButton_OnClicked(object? sender, RoutedEventArgs e) =>
        WorkView.WorkListBox.SelectAll();

    private void InvertSelectionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        var listBox = WorkView.WorkListBox;
        using var operation = listBox.Selection.BatchUpdate();
        for (var i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.Selection.IsSelected(i))
                listBox.Selection.Deselect(i);
            else
                listBox.Selection.Select(i);
        }
    }

    private void CancelSelectionButton_OnClicked(object? sender, RoutedEventArgs e) =>
        WorkView.WorkListBox.UnselectAll();

    private void SortOptionComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e) => SetSortOption();

    private void WorkView_OnDataContextChanged(object? sender, EventArgs args)
    {
        DataContext = (sender as Control)?.DataContext;
        _filterCoordinator.ResetFilterValueCompletions();
        SetSortOption();
    }

    public void SetSortOption()
    {
        if (DataContext is IOperableViewViewModel vm && SortOptionComboBox.GetSelectedValue<LocalSortOption>() is var sortOption)
        {
            vm.SetSortOption(sortOption);
            ScrollToTop();
        }
    }

    private void ScrollToTop()
    {
        if (WorkView.WorkListBox.Scroll is { } scrollView)
            scrollView.Offset = new(0, 0);
    }

    private void AddAllToBookmarkButton_OnClicked(object? sender, RoutedEventArgs e) =>
        _ = WorkContainerBatchOperations.AddAllToBookmarkAsync(this, DataContext as IOperableViewViewModel);

    private void AddAllToBookmarkButton_OnRightClicked(object? sender, ContextRequestedEventArgs e) =>
        _ = WorkContainerBatchOperations.RequestAddToBookmarkWithTagSelectorAsync(AddAllToBookmarkButton, DataContext as IOperableViewViewModel, null);

    private void WorkView_OnRequestAddToBookmark(Control sender, object e) =>
        _ = WorkContainerBatchOperations.RequestAddToBookmarkWithTagSelectorAsync(sender, DataContext as IOperableViewViewModel, e);

    private void SaveAllButton_OnClicked(object? sender, RoutedEventArgs e) =>
        _ = WorkContainerBatchOperations.SaveAllAsync(this, DataContext as IOperableViewViewModel);

    private void OpenAllInBrowserButton_OnClicked(object? sender, RoutedEventArgs e) =>
        _ = WorkContainerBatchOperations.OpenAllInBrowserAsync(this, DataContext as IOperableViewViewModel);

    private void RefreshButton_OnClick(object? sender, RoutedEventArgs e) => RefreshRequested?.Invoke(sender, e);

    private void WorkFilterAutoSuggestBox_OnSearchRequested(object? sender, WorkFilterSearchRequestedEventArgs e) =>
        _filterCoordinator.PerformSearch(DataContext as IOperableViewViewModel, WorkFilterAutoSuggestBox, TopLevel.GetTopLevel(this)?.ViewContainer, e.Text, e.CaretIndex);

    private FilterAnalysisResult AnalyzeFilter(string? text, int caret) =>
        _filterCoordinator.AnalyzeFilter(DataContext as IOperableViewViewModel, text, caret);

    public void ResetEngine(IAsyncEnumerable<object> newEngine) => WorkView.ResetEngine(newEngine);

    public void SetViewModel(IWorkViewViewModel viewModel) => WorkView.SetViewModel(viewModel);

    public void SetSource(IReadOnlyCollection<object> source, SimpleWorkType workType, bool needRefreshOnOpen = false) =>
        WorkView.SetSource(source, workType, needRefreshOnOpen);

    public static readonly FuncValueConverter<int, string> CancelSelectionButtonConverter = new(i => i > 0
        ? I18NManager.GetResource(WorkContainerResources.CancelSelectionButtonFormatted, i)
        : I18NManager.GetResource(WorkContainerResources.CancelSelectionButtonDefaultLabel));
}
