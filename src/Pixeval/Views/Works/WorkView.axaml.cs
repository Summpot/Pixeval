// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.Config;
using Pixeval.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Works;

public sealed partial class WorkView : UserControl, IDisposable
{
    public static readonly StyledProperty<ThumbnailLayoutType> LayoutTypeProperty =
        AvaloniaProperty.Register<WorkView, ThumbnailLayoutType>(
            nameof(LayoutType),
            ThumbnailLayoutType.LinedFlow);

    public ThumbnailLayoutType LayoutType
    {
        get => GetValue(LayoutTypeProperty);
        set => SetValue(LayoutTypeProperty, value);
    }

    private INavigationService NavigationService => this.GetRequiredUiService<INavigationService>();

    private bool _isDisposed;
    private ISimpleViewViewModel? _ownedViewModel;
    private IAsyncEnumerable<object>? _pendingSource;
    private SimpleWorkKind _pendingWorkType = SimpleWorkKind.Illustration;

    public static FuncValueConverter<bool, SelectionMode> SelectionModeConverter { get; } =
        new(b => b ? SelectionMode.Multiple : SelectionMode.Single);

    static WorkView()
    {
        LayoutTypeProperty.Changed.AddClassHandler<WorkView>((view, _) => view.UpdateLayoutPseudoClasses());
    }

    private void StyledElement_OnDataContextChanged(object? sender, EventArgs e) => UpdateLayoutPseudoClasses();

    public WorkView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_isDisposed && change.Property == UiServices.ProviderProperty && _pendingSource is { } source
            && this.GetUiService<AppSettings>() is not null)
            ResetSource(source, _pendingWorkType);
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
            case (Mako.Novel novel, NovelViewViewModel viewViewModel):
                NavigationService.NavigateToNovel(novel, (IReadOnlyList<Mako.Novel>) viewViewModel.View, false, this);
                break;
            case (Mako.Novel novel, SimpleOperableViewViewModel<Mako.Novel> viewViewModel):
                NavigationService.NavigateToNovel(novel, viewViewModel.View.OfType<Mako.Novel>().ToList(), viewViewModel.NeedRefreshOnOpen, this);
                break;
            case (object illustration, IllustrationViewViewModel viewViewModel):
                NavigationService.NavigateToIllustration(illustration, viewViewModel.View, false, this);
                break;
            case (object illustration, SimpleOperableViewViewModel<object> viewViewModel):
                NavigationService.NavigateToIllustration(illustration, viewViewModel.View, viewViewModel.NeedRefreshOnOpen, this);
                break;
            case (Mako.Novel { Id: var id }, _):
                NavigationService.NavigateToNovel(id, this);
                break;
            case (object illustration, _):
                NavigationService.NavigateToIllustration(illustration, null, false, this);
                break;
        }
    }

    /// <summary>
    /// Resets the source of the model owned by this view, creating it if necessary.
    /// </summary>
    public void ResetSource(IAsyncEnumerable<object> source, SimpleWorkKind workType)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        // Pages configure their source in their constructors, before inheriting UI services
        // from the shell. Create the owned model once those services reach this control.
        if (this.GetUiService<AppSettings>() is null)
        {
            _pendingSource = source;
            _pendingWorkType = workType;
            return;
        }

        _pendingSource = null;
        var isNovel = workType is SimpleWorkKind.Novel;
        var viewModel = _ownedViewModel as IWorkViewViewModel;
        switch (viewModel)
        {
            case NovelViewViewModel when isNovel:
            case IllustrationViewViewModel when !isNovel:
                viewModel.ResetSource(source);
                break;
            default:
                var settings = this.GetRequiredUiService<AppSettings>();
                var blocked = this.GetUiService<IBlockedContentService>();
                IWorkViewViewModel newViewModel = isNovel
                    ? new NovelViewViewModel(settings, blocked)
                    : new IllustrationViewViewModel(settings, blocked);
                newViewModel.ResetSource(source);
                SetOwnedViewModel(newViewModel);
                break;
        }
    }

    public void SetSource(IReadOnlyCollection<object> source, SimpleWorkKind workType, bool needRefreshOnOpen = false)
    {
        var blocked = this.GetUiService<IBlockedContentService>();
        IOperableViewViewModel viewModel = workType is SimpleWorkKind.Novel
            ? new SimpleOperableViewViewModel<Mako.Novel>(source.Where(static s => s is Mako.Novel).ToList(), blocked, needRefreshOnOpen)
            : new SimpleOperableViewViewModel<object>(source, blocked, needRefreshOnOpen);
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
        _pendingSource = null;
        var oldViewModel = _ownedViewModel;
        _ownedViewModel = viewModel;
        DataContext = viewModel;
        if (!ReferenceEquals(oldViewModel, viewModel))
            (oldViewModel as IDisposable)?.Dispose();
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
        _pendingSource = null;
        WorkListBox.ItemsSource = null;
        var viewModel = _ownedViewModel;
        _ownedViewModel = null;
        DataContext = null!;
        (viewModel as IDisposable)?.Dispose();
    }

    #endregion
}
