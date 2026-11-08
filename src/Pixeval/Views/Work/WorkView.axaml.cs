// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Misaki;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Work;

public sealed partial class WorkView : UserControl, IDisposable
{
    private bool _isDisposed;

    public event EventHandler<Control, IWorkViewModel>? RequestAddToBookmark;

    public ThumbnailLayoutType LayoutType
    {
        get;
        set
        {
            field = value;
            UpdateLayoutPseudoClasses();
        }
    }

    public static FuncValueConverter<bool, SelectionMode> SelectionModeConverter { get; } =
        new(b => b ? SelectionMode.Multiple : SelectionMode.Single);

    private void StyledElement_OnDataContextChanged(object? sender, EventArgs e) => UpdateLayoutPseudoClasses();

    public WorkView() => InitializeComponent();

    private void UpdateLayoutPseudoClasses()
    {
        var isNovel = DataContext is IOperableViewViewModel { RequireAdaptiveGrid: true };
        PseudoClasses.Set(":novel", isNovel);
        PseudoClasses.Set(":linedFlow", !isNovel && LayoutType is ThumbnailLayoutType.LinedFlow);
        PseudoClasses.Set(":verticalStack", !isNovel && LayoutType is ThumbnailLayoutType.VerticalStack);
        PseudoClasses.Set(":grid", !isNovel && LayoutType is ThumbnailLayoutType.Grid);
        PseudoClasses.Set(":masonry", !isNovel && LayoutType is ThumbnailLayoutType.Masonry);
    }

    private async void WorkItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not WorkItem { DataContext: IWorkViewModel vm } card
            || !ReferenceEquals(e.Source, card)
            || card.FindAncestorOfType<ListBoxItem>() is not { } lbi)
            return;

        e.Handled = true;
        if (WorkListBox.SelectionMode.HasFlag(SelectionMode.Multiple))
        {
            UpdateSelection(lbi, card.ActivationModifiers);
            return;
        }

        await CreateWorkViewerPage(vm);
    }

    private void UpdateSelection(ListBoxItem item, KeyModifiers modifiers)
    {
        var index = WorkListBox.IndexFromContainer(item);
        var anchorIndex = WorkListBox.Selection.AnchorIndex;

        if (!modifiers.HasFlag(KeyModifiers.Shift) || index < 0 || anchorIndex < 0)
        {
            item.IsSelected = !item.IsSelected;
            return;
        }

        using var operation = WorkListBox.Selection.BatchUpdate();
        WorkListBox.Selection.Clear();
        WorkListBox.Selection.SelectRange(anchorIndex, index);
    }

    private async void WorkItem_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: IWorkViewModel vm })
            return;

        var button = e.Source as Button ?? (e.Source as Visual)?.FindAncestorOfType<Button>();
        if (button is not WorkItem)
            return;

        if (!WorkListBox.SelectionMode.HasFlag(SelectionMode.Multiple))
            return;

        await CreateWorkViewerPage(vm);
    }

    private async void WorkItem_OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Arrow navigation focuses the ListBoxItem itself, rather than its card.
        // Child buttons keep their own keyboard handling and must not navigate twice.
        if (sender is not ListBoxItem { DataContext: IWorkViewModel vm } item
            || !ReferenceEquals(e.Source, item) || e.Key is not Key.Enter
            || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
            return;

        e.Handled = true;
        if (WorkListBox.SelectionMode.HasFlag(SelectionMode.Multiple))
            UpdateSelection(item, e.KeyModifiers);
        else
            await CreateWorkViewerPage(vm);
    }

    private async Task CreateWorkViewerPage(IWorkViewModel vm)
    {
        if (TopLevel.GetTopLevel(this) is not { ViewContainer: { } viewContainer })
            return;

        switch (vm, DataContext)
        {
            case (NovelItemViewModel viewModel, NovelViewViewModel viewViewModel):
                viewContainer.CreateNovelPage(viewModel, viewViewModel.DataProvider.CloneRef());
                break;
            case (NovelItemViewModel viewModel, SimpleOperableViewViewModel<NovelItemViewModel> viewViewModel):
                viewContainer.CreateNovelPage(viewModel, viewViewModel.SourceView.CloneSourceView(), viewViewModel.NeedRefreshOnOpen);
                break;
            case (IllustrationItemViewModel viewModel, IllustrationViewViewModel viewViewModel):
                viewContainer.CreateIllustrationPage(viewModel, viewViewModel.DataProvider.CloneRef());
                break;
            case (IllustrationItemViewModel viewModel, SimpleOperableViewViewModel<IllustrationItemViewModel> viewViewModel):
                viewContainer.CreateIllustrationPage(viewModel, viewViewModel.SourceView.CloneSourceView(), viewViewModel.NeedRefreshOnOpen);
                break;
            case (NovelItemViewModel { Entry.Id: var id }, _):
                viewContainer.CreateNovelPage(id);
                break;
            case (IllustrationItemViewModel { Entry: Illustration illustration }, _):
                viewContainer.CreateIllustrationPage(illustration);
                break;
        }
    }

    /// <summary>
    /// 在调用<see cref="ResetEngine"/>前<see cref="Control.DataContext"/>为<see langword="null"/>
    /// </summary>
    public void ResetEngine(IAsyncEnumerable<IArtworkInfo> newEngine)
    {
        var isNovelEngine = newEngine is IAsyncEnumerable<Novel>;
        var viewModel = DataContext as IWorkViewViewModel;
        switch (viewModel)
        {
            case NovelViewViewModel when isNovelEngine:
            case IllustrationViewViewModel when !isNovelEngine:
                viewModel.ResetEngine(newEngine);
                break;
            default:
                IWorkViewViewModel newViewModel = isNovelEngine ? new NovelViewViewModel() : new IllustrationViewViewModel();
                newViewModel.ResetEngine(newEngine);
                SetOwnedViewModel(newViewModel);
                break;
        }
    }

    public void SetSource(IReadOnlyCollection<IArtworkInfo> source, SimpleWorkType workType, bool needRefreshOnOpen = false)
    {
        IOperableViewViewModel viewModel = workType is SimpleWorkType.Novel
            ? new SimpleOperableViewViewModel<NovelItemViewModel>(source, needRefreshOnOpen)
            : new SimpleOperableViewViewModel<IllustrationItemViewModel>(source, needRefreshOnOpen);
        SetOwnedViewModel(viewModel);
    }

    /// <summary>
    /// Takes ownership of <paramref name="viewModel"/> and disposes it when it is replaced or this view is disposed.
    /// </summary>
    public void SetViewModel(IWorkViewViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        SetOwnedViewModel(viewModel);
    }

    private void SetOwnedViewModel(ISimpleViewViewModel viewModel)
    {
        var oldViewModel = DataContext as IDisposable;
        DataContext = viewModel;
        oldViewModel?.Dispose();
    }

    private void WorkItem_OnRequestAddToBookmark(Control sender, IWorkViewModel e) => RequestAddToBookmark?.Invoke(sender, e);

    public void WorkItem_OnRequestOpenUserInfoPage(Control sender, IWorkViewModel e)
    {
        if (e is { Entry: WorkBase { User.Id: var id } })
        {
            if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer)
                viewContainer.CreateUserPage(id);
        }
    }

    private void WorkItem_OnRequestBlockAuthor(Control sender, IWorkViewModel e)
    {
        if (e is not { Entry: WorkBase { User: { } user } } || BlockedContentHelper.IsBlockedPlaceholder(e.Entry))
            return;

        // 即使已在黑名单中（如在其他页面屏蔽后尚未刷新），也刷新当前列表
        var added = BlockedContentHelper.TryAddOrUpdateBlockedUser(user);
        (DataContext as IOperableViewViewModel)?.ReplaceBlockedEntries();

        if (added)
            TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(
                I18NManager.GetResource(BlockedContentResources.AuthorBlockedFormatted, user.Name));
    }

    private void ListBox_OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem lbi)
            return;
        lbi.AddHandler(KeyDownEvent, WorkItem_OnKeyDown, RoutingStrategies.Tunnel);
        lbi.DoubleTapped += WorkItem_OnDoubleTapped;
    }

    private void ListBox_OnContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container is not ListBoxItem lbi)
            return;

        lbi.RemoveHandler(KeyDownEvent, WorkItem_OnKeyDown);
        lbi.DoubleTapped -= WorkItem_OnDoubleTapped;
    }

    #region Disposal

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, this));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        WorkListBox.ItemsSource = null;
        RequestAddToBookmark = null;
        var d = DataContext;
        DataContext = null!;
        if (d is IDisposable viewModel)
            viewModel.Dispose();
    }

    #endregion
}
