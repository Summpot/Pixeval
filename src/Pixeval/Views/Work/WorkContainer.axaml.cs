// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Mako;
using Mako.Global.Enum;
using Mako.Model;
using Misaki;
using Pixeval.Collections;
using Pixeval.Controls;
using Pixeval.Filters.Analysis;
using Pixeval.I18N;
using Pixeval.Models.Filters;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Work;

/// <summary>
/// 所有插画集合通用的容器
/// </summary>
public partial class WorkContainer : UserControl
{
    private const string FilterDiagnosticResourcePrefix = "Filter.Diagnostics.";

    private IReadOnlyCollection<IWorkViewModel>? _filterCompletionSource;
    private int _filterCompletionSourceCount = -1;
    private IReadOnlyList<FilterCompletionDefinition> _tagValueCompletions = [];
    private IReadOnlyList<FilterCompletionDefinition> _authorValueCompletions = [];
    private CancellationTokenSource? _bookmarkLifetime;
    private object? _bookmarkBatch;
    private MakoClient? _bookmarkClient;
    private EventHandler<MakoClient, TokenResponse?>? _bookmarkAccountObserver;
    private long? _bookmarkAccountId;
    private BookmarkAccountState? _bookmarkAccountState;
    private long _bookmarkLifetimeAccountEpoch;

    private sealed class BookmarkAccountState(long accountId)
    {
        public long AccountId = accountId;
        public long Epoch;
    }

    public static readonly DirectProperty<WorkContainer, bool> IsBookmarkBatchPendingProperty =
        AvaloniaProperty.RegisterDirect<WorkContainer, bool>(nameof(IsBookmarkBatchPending), o => o.IsBookmarkBatchPending);

    public bool IsBookmarkBatchPending
    {
        get;
        private set => SetAndRaise(IsBookmarkBatchPendingProperty, ref field, value);
    }

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
            vm.SetSortDescriptions(MakoHelper.GetSortDescription(sortOption));

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
        if (IsLoaded && DataContext is IOperableViewViewModel viewModel)
            await AddBatchToBookmarkAsync([.. viewModel.SelectedEntries], (false, null), BookmarkToken);
    }

    private async void AddAllToBookmarkButton_OnRightClicked(object? sender, ContextRequestedEventArgs e)
    {
        await ShowBookmarkTagSelectorAsync(AddAllToBookmarkButton, null);
    }

    private async void WorkView_OnRequestAddToBookmark(Control sender, IWorkViewModel e)
    {
        await ShowBookmarkTagSelectorAsync(sender, e);
    }

    private async Task ShowBookmarkTagSelectorAsync(Control placementTarget, IWorkViewModel? target)
    {
        if (!IsLoaded || target is null && (IsBookmarkBatchPending
            || DataContext is not IOperableViewViewModel { SelectedEntries.Count: > 0 }))
            return;

        var token = BookmarkToken;
        var accountId = _bookmarkAccountId;
        var accountEpoch = _bookmarkLifetimeAccountEpoch;
        var accountState = _bookmarkAccountState;
        IWorkViewModel[] targets = target is null && DataContext is IOperableViewViewModel viewModel
            ? [.. viewModel.SelectedEntries]
            : [];
        var id = target is { Entry.Id: { } idStr } && long.TryParse(idStr, out var idLong) ? idLong : 0;
        var type = target is NovelItemViewModel || DataContext is NovelViewViewModel or SimpleOperableViewViewModel<NovelItemViewModel>
            ? SimpleWorkType.Novel
            : SimpleWorkType.Illustration;

        await BookmarkTagSelectorFlyoutHelper.ShowAsync(
            placementTarget,
            type,
            id,
            async e =>
            {
                if (!IsCurrentBookmark(token, accountId, accountEpoch, accountState))
                    return;
                if (target is null)
                    await AddBatchToBookmarkAsync(targets, e, token);
                else
                {
                    await target.AddToBookmarkCommand.ExecuteAsync((e.Tags, e.IsPrivate, this));
                    if (IsCurrentBookmark(token, accountId, accountEpoch, accountState) && target.LastBookmarkSucceeded)
                        TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(I18NManager.GetResource(MiscResources.AddedToBookmark));
                }
            },
            PlacementMode.Bottom, token);
    }

    private CancellationToken BookmarkToken
    {
        get
        {
            // A token event can arrive from the OAuth worker. Catch an account
            // change here as well, before accepting another UI action.
            var accountEpoch = _bookmarkAccountState is { } state ? Volatile.Read(ref state.Epoch) : 0;
            if (_bookmarkAccountId != _bookmarkClient?.Me?.Id || _bookmarkLifetimeAccountEpoch != accountEpoch)
            {
                CancelBookmark();
                _bookmarkAccountId = _bookmarkClient?.Me?.Id;
                _bookmarkLifetimeAccountEpoch = accountEpoch;
            }
            return (_bookmarkLifetime ??= new()).Token;
        }
    }

    private bool IsCurrentBookmark(CancellationToken token, long? accountId, long accountEpoch, BookmarkAccountState? accountState) =>
        !token.IsCancellationRequested && _bookmarkClient is { } client && client.Me?.Id == accountId
        && accountState is { } state && ReferenceEquals(_bookmarkAccountState, state)
        && Volatile.Read(ref state.Epoch) == accountEpoch;

    private async Task AddBatchToBookmarkAsync(IReadOnlyList<IWorkViewModel> targets,
        (bool IsPrivate, IReadOnlyList<string>? Tags) selection, CancellationToken token)
    {
        if (IsBookmarkBatchPending || token.IsCancellationRequested || targets.Count == 0)
            return;

        var batch = new object();
        var accountId = _bookmarkAccountId;
        var accountEpoch = _bookmarkLifetimeAccountEpoch;
        var accountState = _bookmarkAccountState;
        _bookmarkBatch = batch;
        IsBookmarkBatchPending = true;
        var tags = selection.Tags?.ToArray();
        var restoreConfirmationFocus = false;
        try
        {
            if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer
                && targets.Count >= 20
                && await viewContainer.CreateOkCancelAsync(
                    I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.ForBookmarkTitle),
                    I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.Content), token) is not ContentDialogResult.Primary)
            {
                restoreConfirmationFocus = !token.IsCancellationRequested;
                return;
            }

            var completed = 0;
            foreach (var target in targets)
            {
                // A sent write can finish, but leaving this source must not send
                // further writes or report success in a later page lifetime.
                if (!IsCurrentBookmark(token, accountId, accountEpoch, accountState))
                    return;
                if (!target.AddToBookmarkCommand.CanExecute((tags, selection.IsPrivate, this))
                    || target.BookmarkCommand.IsRunning)
                    continue;
                await target.AddToBookmarkCommand.ExecuteAsync((tags, selection.IsPrivate, this));
                if (target.LastBookmarkSucceeded)
                    completed++;
            }
            if (IsCurrentBookmark(token, accountId, accountEpoch, accountState) && completed > 0)
                TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(I18NManager.GetResource(WorkContainerResources.AddedAllToBookmarkContentFormatted, completed));
        }
        finally
        {
            if (ReferenceEquals(_bookmarkBatch, batch))
            {
                _bookmarkBatch = null;
                IsBookmarkBatchPending = false;
                if (restoreConfirmationFocus && !token.IsCancellationRequested && IsLoaded)
                    (AddAllToBookmarkButton.IsEffectivelyVisible && AddAllToBookmarkButton.IsEffectivelyEnabled
                        ? (Control) AddAllToBookmarkButton
                        : SelectionToggleButton).Focus();
            }
        }
    }

    private void CancelBookmark()
    {
        _bookmarkLifetime?.Cancel();
        _bookmarkLifetime?.Dispose();
        _bookmarkLifetime = null;
        _bookmarkBatch = null;
        IsBookmarkBatchPending = false;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        CancelBookmark();
        base.OnDataContextChanged(e);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _bookmarkClient = App.AppViewModel.MakoClient;
        _bookmarkAccountId = _bookmarkClient.Me?.Id;
        var state = new BookmarkAccountState(_bookmarkAccountId ?? 0);
        _bookmarkAccountState = state;
        EventHandler<MakoClient, TokenResponse?> observer = null!;
        observer = (client, response) =>
        {
            if (!ReferenceEquals(Volatile.Read(ref _bookmarkAccountObserver), observer))
                return;
            var accountId = response?.User.Id ?? 0;
            if (Interlocked.Exchange(ref state.AccountId, accountId) == accountId)
                return;
            var lifetime = Volatile.Read(ref _bookmarkLifetime);
            // An already executing observer from a previous attachment only
            // advances its own state, never a newly mounted page's generation.
            Interlocked.Increment(ref state.Epoch);
            Dispatcher.UIThread.Post(() =>
            {
                if (!ReferenceEquals(_bookmarkAccountObserver, observer))
                    return;
                // Do not cancel a new operation accepted after this event, or
                // a same-account OAuth renewal between two batch requests.
                if (ReferenceEquals(_bookmarkLifetime, lifetime))
                    CancelBookmark();
                _bookmarkAccountId = client.Me?.Id;
            });
        };
        _bookmarkAccountObserver = observer;
        _bookmarkClient.TokenRefreshed += observer;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_bookmarkClient is { } client)
            client.TokenRefreshed -= _bookmarkAccountObserver;
        _bookmarkClient = null;
        _bookmarkAccountObserver = null;
        _bookmarkAccountState = null;
        CancelBookmark();
        base.OnUnloaded(e);
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
            i.SaveCommand.Execute(null);

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
            _ = await TopLevel.GetTopLevel(this)!.Launcher.LaunchUriAsync(selectedEntry.Entry.WebsiteUri);
        }
    }

    private void RefreshButton_OnClick(object? sender, RoutedEventArgs e) => RefreshRequested?.Invoke(sender, e);

    private void CancelSelectionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        var wasOpen = SelectionToolBar.IsOpen;
        WorkView.WorkListBox.UnselectAll();
        if (wasOpen)
        {
            // Clearing disables the focused overflow command and every following
            // action. Close the menu and keep keyboard focus on a live control.
            SelectionToolBar.IsOpen = false;
            SelectionToggleButton.Focus();
        }
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

        viewModel.UserFilter = query.HasPredicates
            ? IFilter<IWorkViewModel>.Create(o => o.Filter(query.Root), false)
            : null;
        WorkFilterAutoSuggestBox.ClearSelection();
    }

    private FilterAnalysisResult AnalyzeFilter(string? text, int caret)
        => WorkFilterLanguage.Instance.Analyze(text, caret, GetFilterValueCompletions);

    private IReadOnlyList<FilterCompletionDefinition> GetFilterValueCompletions(FilterValueCompletionContext context)
    {
        if (DataContext is not IOperableViewViewModel { Source.Count: > 0 } viewModel)
            return [];

        EnsureFilterValueCompletions(viewModel.Source);
        return context.Match.Syntax.Key switch
        {
            WorkTagFilterSyntax.KeyConst => _tagValueCompletions,
            WorkAuthorFilterSyntax.KeyConst => _authorValueCompletions,
            _ => []
        };
    }

    private void EnsureFilterValueCompletions(IReadOnlyCollection<IWorkViewModel> source)
    {
        if (ReferenceEquals(_filterCompletionSource, source) && _filterCompletionSourceCount == source.Count)
            return;

        var tags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var authors = new HashSet<IUser>();
        foreach (var work in source)
        {
            foreach (var author in work.Entry.Authors)
                _ = authors.Add(author);

            foreach (var tagGroup in work.Entry.Tags)
            {
                foreach (var tag in tagGroup)
                {
                    if (string.IsNullOrWhiteSpace(tag.Name))
                        continue;

                    if (tags.TryGetValue(tag.Name, out var description) && !string.IsNullOrWhiteSpace(description))
                        continue;

                    tags[tag.Name] = string.IsNullOrWhiteSpace(tag.TranslatedName) || string.Equals(tag.Name, tag.TranslatedName, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : tag.TranslatedName;
                }
            }
        }

        _tagValueCompletions = [.. tags.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => new FilterCompletionDefinition($"tag:{pair.Key}", pair.Key, pair.Key, pair.Value))];
        _authorValueCompletions = [.. authors.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Select(a => new FilterCompletionDefinition($"author:{a.Name}", a.Name, a.Name, a is UserInfo user ? user.Account : null))];
        _filterCompletionSource = source;
        _filterCompletionSourceCount = source.Count;
    }

    private void ResetFilterValueCompletions()
    {
        _filterCompletionSource = null;
        _filterCompletionSourceCount = -1;
        _tagValueCompletions = [];
        _authorValueCompletions = [];
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

    public void ResetEngine(IAsyncEnumerable<IArtworkInfo> newEngine)
    {
        CancelBookmark();
        WorkView.ResetEngine(newEngine);
    }

    /// <inheritdoc cref="WorkView.SetViewModel" />
    public void SetViewModel(IWorkViewViewModel viewModel)
    {
        WorkView.SetViewModel(viewModel);
    }

    public void SetSource(IReadOnlyCollection<IArtworkInfo> source, SimpleWorkType workType, bool needRefreshOnOpen = false)
    {
        WorkView.SetSource(source, workType, needRefreshOnOpen);
    }

    public static readonly FuncValueConverter<int, string> CancelSelectionButtonConverter = new(i => i > 0
        ? I18NManager.GetResource(WorkContainerResources.CancelSelectionButtonFormatted, i)
        : I18NManager.GetResource(WorkContainerResources.CancelSelectionButtonDefaultLabel));
}
