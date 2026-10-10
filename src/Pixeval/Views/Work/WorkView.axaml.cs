// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Work;

public sealed partial class WorkView : UserControl, IDisposable
{
    private static INavigationService NavigationService =>
        App.Services?.GetService<INavigationService>() ?? new NavigationService();

    private bool _isDisposed;

    public event EventHandler<Control, object>? RequestAddToBookmark;

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

    public WorkView()
    {
        InitializeComponent();
        LayoutType = App.Services!.GetRequiredService<AppSettings>()
            .BrowsingExperienceSettings.ThumbnailLayout.ThumbnailLayoutType;
    }

    private void UpdateLayoutPseudoClasses()
    {
        var isNovel = DataContext is IOperableViewViewModel { RequireAdaptiveGrid: true };
        PseudoClasses.Set(":novel", isNovel);
        PseudoClasses.Set(":linedFlow", !isNovel && LayoutType is ThumbnailLayoutType.LinedFlow);
        PseudoClasses.Set(":verticalStack", !isNovel && LayoutType is ThumbnailLayoutType.VerticalStack);
        PseudoClasses.Set(":grid", !isNovel && LayoutType is ThumbnailLayoutType.Grid);
        PseudoClasses.Set(":masonry", !isNovel && LayoutType is ThumbnailLayoutType.Masonry);
    }

    private void WorkItem_OnTapped(object? sender, TappedEventArgs tappedEventArgs)
    {
        if (sender is not ListBoxItem { DataContext: { } vm } lbi)
            return;

        if (WorkListBox.SelectionMode.HasFlag(SelectionMode.Multiple))
        {
            UpdateSelection(lbi, tappedEventArgs);
            return;
        }

        CreateWorkViewerPage(vm);
    }

    private void UpdateSelection(ListBoxItem item, TappedEventArgs e)
    {
        var index = WorkListBox.IndexFromContainer(item);
        var anchorIndex = WorkListBox.Selection.AnchorIndex;

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) || index < 0 || anchorIndex < 0)
        {
            item.IsSelected = !item.IsSelected;
            return;
        }

        using var operation = WorkListBox.Selection.BatchUpdate();
        WorkListBox.Selection.Clear();
        WorkListBox.Selection.SelectRange(anchorIndex, index);
    }

    private void WorkItem_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: { } vm })
            return;

        if (WorkListBox.SelectionMode.HasFlag(SelectionMode.Single))
            return;

        CreateWorkViewerPage(vm);
    }

    private void CreateWorkViewerPage(object vm)
    {
        switch (vm, DataContext)
        {
            case (Novel novel, NovelViewViewModel viewViewModel):
                NavigationService.NavigateToNovel(novel, (IReadOnlyList<Novel>) viewViewModel.View, false, this);
                break;
            case (Novel novel, SimpleOperableViewViewModel<Novel> viewViewModel):
                NavigationService.NavigateToNovel(novel, viewViewModel.View.OfType<Novel>().ToList(), viewViewModel.NeedRefreshOnOpen, this);
                break;
            case (object illustration, IllustrationViewViewModel viewViewModel):
                NavigationService.NavigateToIllustration(illustration, viewViewModel.View, false, this);
                break;
            case (object illustration, SimpleOperableViewViewModel<object> viewViewModel):
                NavigationService.NavigateToIllustration(illustration, viewViewModel.View, viewViewModel.NeedRefreshOnOpen, this);
                break;
            case (Novel { Id: var id }, _):
                NavigationService.NavigateToNovel(id, this);
                break;
            case (object illustration, _):
                NavigationService.NavigateToIllustration(illustration, null, false, this);
                break;
        }
    }

    /// <summary>
    /// 在调用<see cref="ResetEngine"/>前<see cref="Control.DataContext"/>为<see langword="null"/>
    /// </summary>
    public void ResetEngine(IAsyncEnumerable<object> newEngine)
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

    public void SetSource(IReadOnlyCollection<object> source, SimpleWorkType workType, bool needRefreshOnOpen = false)
    {
        IOperableViewViewModel viewModel = workType is SimpleWorkType.Novel
            ? new SimpleOperableViewViewModel<Novel>(source.OfType<Novel>().ToList(), needRefreshOnOpen)
            : new SimpleOperableViewViewModel<object>(source, needRefreshOnOpen);
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

    private void WorkItem_OnRequestAddToBookmark(Control sender, object e) => RequestAddToBookmark?.Invoke(sender, e);

    public void WorkItem_OnRequestOpenUserInfoPage(Control sender, object e)
    {
        if (e is IWorkEntry { User.Id: var id })
        {
            NavigationService.NavigateToUser(id, this);
        }
    }

    private void ListBox_OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem lbi)
            return;
        lbi.Tapped += WorkItem_OnTapped;
        lbi.DoubleTapped += WorkItem_OnDoubleTapped;
    }

    private void ListBox_OnContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container is not ListBoxItem lbi)
            return;

        lbi.Tapped -= WorkItem_OnTapped;
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
