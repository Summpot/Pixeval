// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.Collections;
using Pixeval.Controls;
using Pixeval.Filters;
using Pixeval.Native.Filters;
using Pixeval.I18N;
using Pixeval.Models.Filters;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Work;

/// <summary>
/// 所有插画集合通用的容器
/// </summary>
public partial class WorkContainer : UserControl
{
    private const string FilterDiagnosticResourcePrefix = "Filter.Diagnostics.";

    private IReadOnlyCollection<object>? _filterCompletionSource;
    private int _filterCompletionSourceCount = -1;

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

    /// <summary>
    /// The command elements that will appear at the left of the TopCommandBar
    /// </summary>
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

    private void SelectAllToggleButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        WorkView.WorkListBox.SelectAll();
    }

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

    private void SortOptionComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e) => SetSortOption();

    private void WorkView_OnDataContextChanged(object? sender, EventArgs args)
    {
        DataContext = (sender as Control)?.DataContext;
        ResetFilterValueCompletions();
        SetSortOption();
    }

    public void SetSortOption()
    {
        if (DataContext is IOperableViewViewModel vm && SortOptionComboBox.GetSelectedValue<LocalSortOption>() is var sortOption)
        {
            vm.SetSortDescriptions(ArtworkInfoExtensions.GetSortDescription(sortOption));

            ScrollToTop();
        }
    }

    private void ScrollToTop()
    {
        if (WorkView.WorkListBox.Scroll is { } scrollView)
            scrollView.Offset = new(0, 0);
    }

    private async void AddAllToBookmarkButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        await AddToBookmarkAsync(null, (false, null)).ConfigureAwait(false);
    }

    private async void AddAllToBookmarkButton_OnRightClicked(object? sender, ContextRequestedEventArgs e)
    {
        await ShowBookmarkTagSelectorAsync(AddAllToBookmarkButton, null).ConfigureAwait(false);
    }

    private async void WorkView_OnRequestAddToBookmark(Control sender, object e)
    {
        await ShowBookmarkTagSelectorAsync(sender, e).ConfigureAwait(false);
    }

    private async Task ShowBookmarkTagSelectorAsync(Control placementTarget, object? target)
    {
        if (target is null && DataContext is not IOperableViewViewModel { SelectedEntries.Count: > 0 })
            return;

        var id = target switch
        {
            Illustration illust => illust.Id,
            Novel novel => novel.Id,
            BooruPost booru => long.TryParse(booru.Id, out var idLong) ? idLong : 0,
            _ => 0
        };
        var type = target is Novel || DataContext is NovelViewViewModel or SimpleOperableViewViewModel<Novel>
            ? SimpleWorkType.Novel
            : SimpleWorkType.Illustration;

        await BookmarkTagSelectorFlyoutHelper.ShowAsync(
            placementTarget,
            type,
            id,
            async e => await AddToBookmarkAsync(target, e),
            PlacementMode.Bottom);
    }

    private async Task AddToBookmarkAsync(object? target, (bool IsPrivate, IReadOnlyList<string>? Tags) e)
    {
        if (target is IWorkEntry workTarget)
        {
            await WorkCommands.AddToBookmarkCommand.ExecuteAsync(new BookmarkRequest(workTarget, e.IsPrivate, e.Tags));
            TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(I18NManager.GetResource(MiscResources.AddedToBookmark));
            return;
        }

        if (DataContext is not IOperableViewViewModel viewModel)
            return;

        if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer
            && viewModel.SelectedEntries.Count >= 20
            && await viewContainer.CreateOkCancelAsync(
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.ForBookmarkTitle),
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.Content)) is not ContentDialogResult.Primary)
            return;

        foreach (var i in viewModel.SelectedEntries)
        {
            if (i is IWorkEntry work)
                await WorkCommands.AddToBookmarkCommand.ExecuteAsync(new BookmarkRequest(work, e.IsPrivate, e.Tags));
        }
        if (viewModel.SelectedEntries.Count is var c and > 0)
            TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(I18NManager.GetResource(WorkContainerResources.AddedAllToBookmarkContentFormatted, c));
    }

    private async void SaveAllButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IOperableViewViewModel viewModel)
            return;

        if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer
            && viewModel.SelectedEntries.Count >= 20
            && await viewContainer.CreateOkCancelAsync(
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.ForSaveTitle),
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.Content)) is not ContentDialogResult.Primary)
            return;

        foreach (var i in viewModel.SelectedEntries)
            WorkCommands.SaveCommand.Execute(i);

        TopLevel.GetTopLevel(this)?.ViewContainer?.ShowInformation(
            I18NManager.GetResource(WorkContainerResources.DownloadItemsQueuedFormatted, viewModel.SelectedEntries.Count));
    }

    private async void OpenAllInBrowserButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IOperableViewViewModel viewModel)
            return;

        if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer
            && viewModel.SelectedEntries.Count > 15
            && await viewContainer.CreateOkCancelAsync(
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.ForOpenInBrowser.Title),
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.ForOpenInBrowser.Content)) is not ContentDialogResult.Primary)
            return;

        foreach (var selectedEntry in viewModel.SelectedEntries)
        {
            var uri = selectedEntry switch
            {
                Illustration illust => illust.WebsiteUri,
                Novel novel => novel.WebsiteUri,
                BooruPost booru => booru.WebsiteUri,
                _ => null
            };
            if (uri is not null)
                _ = await TopLevel.GetTopLevel(this)!.Launcher.LaunchUriAsync(uri);
        }
    }

    private void RefreshButton_OnClick(object? sender, RoutedEventArgs e) => RefreshRequested?.Invoke(sender, e);

    private void CancelSelectionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        WorkView.WorkListBox.UnselectAll();
    }

    private void WorkFilterAutoSuggestBox_OnSearchRequested(object? sender, WorkFilterSearchRequestedEventArgs e)
        => PerformSearch(e.Text, e.CaretIndex);

    private void PerformSearch(string? text, int caret)
    {
        if (DataContext is not IOperableViewViewModel viewModel)
            return;

        if (string.IsNullOrWhiteSpace(text))
        {
            viewModel.UserFilter = null;
            WorkFilterAutoSuggestBox.ClearSelection();
            WorkFilterAutoSuggestBox.RefreshCompletions();
            return;
        }

        var analysis = AnalyzeFilter(text, caret);
        WorkFilterAutoSuggestBox.RefreshCompletions(analysis);
        if (!analysis.IsSuccess || analysis.Query is not { } query)
        {
            if (analysis.Diagnostics.Count > 0)
            {
                WorkFilterAutoSuggestBox.HighlightDiagnostic(analysis.Diagnostics[0].Span);
                TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(
                    I18NManager.GetResource(FilterResources.FilterQueryError),
                    FormatDiagnosticMessage(analysis));
            }

            return;
        }

        viewModel.UserFilter = query.HasPredicates()
            ? IFilter<object>.Create(o => query.MatchesArtwork(o.ToArtworkMetadata()), false)
            : null;
        WorkFilterAutoSuggestBox.ClearSelection();
    }

    private FilterAnalysisResult AnalyzeFilter(string? text, int caret)
    {
        if (DataContext is IOperableViewViewModel { Source.Count: > 0 } viewModel)
            EnsureFilterValueCompletions(viewModel.Source);

        return WorkFilterLanguage.CompletionEngine.Analyze(text ?? string.Empty, caret);
    }

    private void EnsureFilterValueCompletions(IReadOnlyCollection<object> source)
    {
        if (ReferenceEquals(_filterCompletionSource, source) && _filterCompletionSourceCount == source.Count)
            return;

        _filterCompletionSource = source;
        _filterCompletionSourceCount = source.Count;
        WorkFilterLanguage.CompletionEngine.SetSessionArtworks(source.Select(w => w.ToArtworkMetadata()).ToList());
    }

    private void ResetFilterValueCompletions()
    {
        _filterCompletionSource = null;
        _filterCompletionSourceCount = -1;
        WorkFilterLanguage.CompletionEngine.ClearSessionCandidates();
    }

    private static string FormatDiagnosticMessage(FilterAnalysisResult analysis)
    {
        var diagnostic = analysis.Diagnostics[0];
        var completionSuffix = analysis.Completions.Count > 0
            ? I18NManager.GetResource(
                FilterResources.Diagnostics.CompletionSuffixFormatted,
                string.Join(", ", analysis.Completions.Select(t => t.DisplayText).Distinct(StringComparer.OrdinalIgnoreCase).Take(6)))
            : "";
        return I18NManager.GetResource(
            FilterResources.Diagnostics.MessageWithPositionFormatted,
            FormatDiagnostic(diagnostic),
            diagnostic.Span.Start + 1,
            completionSuffix);
    }

    private static string FormatDiagnostic(FilterDiagnostic diagnostic)
    {
        var arguments = diagnostic.Arguments;
        return arguments.Count > 0
            ? I18NManager.GetResource(GetFilterDiagnosticResourceKey(diagnostic.Kind), [.. arguments])
            : I18NManager.GetResource(GetFilterDiagnosticResourceKey(diagnostic.Kind));
    }

    private static string GetFilterDiagnosticResourceKey(FilterDiagnosticKind kind) => FilterDiagnosticResourcePrefix + kind;

    public void ResetEngine(IAsyncEnumerable<object> newEngine)
    {
        WorkView.ResetEngine(newEngine);
    }

    /// <inheritdoc cref="WorkView.SetViewModel" />
    public void SetViewModel(IWorkViewViewModel viewModel)
    {
        WorkView.SetViewModel(viewModel);
    }

    public void SetSource(IReadOnlyCollection<object> source, SimpleWorkType workType, bool needRefreshOnOpen = false)
    {
        WorkView.SetSource(source, workType, needRefreshOnOpen);
    }

    public static readonly FuncValueConverter<int, string> CancelSelectionButtonConverter = new(i => i > 0
        ? I18NManager.GetResource(WorkContainerResources.CancelSelectionButtonFormatted, i)
        : I18NManager.GetResource(WorkContainerResources.CancelSelectionButtonDefaultLabel));
}
