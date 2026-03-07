using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Mako.Engine;
using Mako.Global.Enum;
using Mako.Model;
using Misaki;
using Pixeval.AppManagement;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.WorkDetails;
using Pixeval.Views.Capability;

namespace Pixeval.Views.Work;

public partial class WorkView : UserControl, IStructuralDisposalCompleter//, IEntryView<ISortableEntryViewViewModel>
{
    private const double ViewportRecyclePreloadFactor = 1.5;

    private const double ViewportRecyclePreloadMin = 480;

    private static readonly TimeSpan ThumbnailRetryDelay = TimeSpan.FromSeconds(5);

    public event EventHandler<WorkView, IWorkViewModel>? RequestAddToBookmark;

    public static readonly DirectProperty<WorkView, double> ItemWidthProperty =
        AvaloniaProperty.RegisterDirect<WorkView, double>(nameof(ItemWidth), t => t.ItemWidth, (t, v) => t.ItemWidth = v);

    public double ItemWidth
    {
        get;
        set => SetAndRaise(ItemWidthProperty, ref field, value);
    }

    public static FuncValueConverter<bool, SelectionMode> SelectionModeConverter { get; } =
        new(b => b ? SelectionMode.Multiple : SelectionMode.Single);

    public ItemsViewLayoutType LayoutType { get; set; }

    private object ThumbnailReferenceKey { get; } = new();

    private ScrollViewer? _scrollViewer;

    private Vector _savedOffset;

    private bool _hasSavedOffset;

    private bool _restoreOffsetScheduled;

    private bool _syncScheduled;

    private bool _retryScheduled;

    private HashSet<ListBoxItem> TrackedContainers { get; } = [];

    private HashSet<IWorkViewModel> ViewportRetainedEntries { get; } = [];

    public WorkView() => InitializeComponent();

    public static readonly DirectProperty<WorkView, SimpleWorkType> TypeProperty =
        AvaloniaProperty.RegisterDirect<WorkView, SimpleWorkType>(nameof(Type),
            t => t.Type,
            (t, v) => t.Type = v);

    public SimpleWorkType Type
    {
        get;
        private set => SetAndRaise(TypeProperty, ref field, value);
    }

    private void WorkItem_OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not { }
            || sender is not Control { DataContext: IWorkViewModel })
            return;

        ScheduleViewportThumbnailSynchronization();
    }

    private void WorkItem_OnTapped(object? sender, TappedEventArgs tappedEventArgs)
    {
        if (sender is not ListBoxItem { DataContext: IWorkViewModel vm } lbi)
            return;

        if (ListBox.SelectionMode.HasFlag(SelectionMode.Multiple))
        {
            lbi.IsSelected = !lbi.IsSelected;
            return;
        }

        switch (vm, DataContext)
        {
            case (NovelItemViewModel { Entry.Id: var id, Entry.Title: var title }, _):
                NavigateToWorkDetails(id, WorkDetailsKind.Novel, title);
                break;
            case (IllustrationItemViewModel { Entry: Illustration { Id: var id, Title: var title, ImageType: var imageType } }, _):
                NavigateToWorkDetails(
                    id,
                    imageType is ImageType.ImageSet ? WorkDetailsKind.Manga : WorkDetailsKind.Illustration,
                    title);
                break;
        }
    }

    private void NavigateToWorkDetails(long workId, WorkDetailsKind kind, string? title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var viewContainer = topLevel?.ViewContainer;

        if (viewContainer is null)
            return;

        var symbol = kind switch
        {
            WorkDetailsKind.Novel => Symbol.BookNumber,
            WorkDetailsKind.Manga => Symbol.ImageMultiple,
            _ => Symbol.Image
        };

        viewContainer.NavigateTo(
            typeof(WorkDetailsPage),
            new SymbolIcon
            {
                Symbol = symbol,
                FontSize = 16,
                IconVariant = IconVariant.Color
            },
            string.IsNullOrWhiteSpace(title) ? $"作品 {workId}" : title,
            new WorkDetailsNavigationParameter(workId, kind));
    }

    private void WorkItem_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: IWorkViewModel vm })
            return;

        _ = vm;

        _ = ListBox.SelectionMode.HasFlag(SelectionMode.Single);
    }

    private void WorkView_OnSelectionChanged(object? o, SelectionChangedEventArgs selectionChangedEventArgs)
    {
        if (o is not ListBox listBox || DataContext is not ISortableEntryViewViewModel viewModel)
            return;
        if (listBox.SelectedItems is not { Count: > 0 })
        {
            viewModel.SelectedEntries = viewModel switch
            {
                NovelViewViewModel => (NovelItemViewModel[]) [],
                IllustrationViewViewModel => (IllustrationItemViewModel[]) [],
                _ => viewModel.SelectedEntries
            };
            return;
        }

        viewModel.SelectedEntries = viewModel switch
        {
            NovelViewViewModel => [.. listBox.SelectedItems.Cast<NovelItemViewModel>()],
            IllustrationViewViewModel => [.. listBox.SelectedItems.Cast<IllustrationItemViewModel>()],
            _ => viewModel.SelectedEntries
        };
    }

    /// <summary>
    /// 在调用<see cref="ResetEngine"/>前<see cref="StyledElement.DataContext"/>为<see langword="null"/>
    /// </summary>
    public void ResetEngine(IFetchEngine<IArtworkInfo> newEngine, int itemsPerPage = 20, int itemLimit = -1)
    {
        var type = newEngine.GetType().GetInterfaces()[0].GenericTypeArguments.SingleOrDefault();
        var viewModel = DataContext as ISortableEntryViewViewModel;
        switch (viewModel)
        {
            case NovelViewViewModel when type == typeof(Novel):
            case IllustrationViewViewModel when type != typeof(Novel):
                viewModel.ResetEngine(newEngine, itemsPerPage, itemLimit);
                break;
            default:
                if (type == typeof(Novel))
                {
                    Type = SimpleWorkType.Novel;
                    viewModel?.Dispose();
                    ItemWidth = 350;
                    viewModel = new NovelViewViewModel();
                }
                else
                {
                    Type = SimpleWorkType.IllustrationAndManga;
                    viewModel?.Dispose();
                    ItemWidth = LayoutType is ItemsViewLayoutType.Grid ? 240 : double.NaN;
                    viewModel = new IllustrationViewViewModel();
                }

                viewModel.ResetEngine(newEngine, itemsPerPage, itemLimit);
                DataContext = viewModel;
                ListBox.ItemsSource = viewModel.View;

                break;
        }
    }

    private void WorkItem_OnRequestAddToBookmark(Control sender, IWorkViewModel e) => RequestAddToBookmark?.Invoke(this, e);

    public async void WorkItem_OnRequestOpenUserInfoPage(Control sender, IWorkViewModel e)
    {
        if (e is { IsBookmarkSupported: false, Entry: WorkBase { User.Id: var id } })
        {
            _ = id;
            // await TopLevel.GetTopLevel(this)?.ViewContainer.CreateIllustratorPageAsync(id);
        }
    }

    public void CompleteDisposal()
    {
        ReleaseAllRetainedViewportThumbnails();
        var d = DataContext;
        DataContext = null!;
        if (d is not ISortableEntryViewViewModel viewModel)
            return;
        foreach (var vm in viewModel.Source)
            vm.UnloadThumbnail(viewModel);
        viewModel.Dispose();
    }

    public List<Action<IStructuralDisposalCompleter?>> ChildrenCompletes { get; } = [];

    public bool CompleterRegistered { get; set; }

    public bool CompleterDisposed { get; set; }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ((IStructuralDisposalCompleter) this).Hook();
        EnsureScrollViewerAttached();
        ScheduleRestoreOffset();
        ScheduleViewportThumbnailSynchronization();
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_scrollViewer is { } scrollViewer)
        {
            _savedOffset = scrollViewer.Offset;
            _hasSavedOffset = true;
        }

        DetachScrollViewer();
        base.OnUnloaded(e);
    }

    private void ListBox_OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem lbi)
            return;

        _ = TrackedContainers.Add(lbi);

        lbi.Tapped -= WorkItem_OnTapped;
        lbi.DoubleTapped -= WorkItem_OnDoubleTapped;
        lbi.Loaded -= ListBoxItem_OnLoaded;
        lbi.Unloaded -= ListBoxItem_OnUnloaded;
        lbi.Tapped += WorkItem_OnTapped;
        lbi.DoubleTapped += WorkItem_OnDoubleTapped;
        lbi.Loaded += ListBoxItem_OnLoaded;
        lbi.Unloaded += ListBoxItem_OnUnloaded;

        ScheduleViewportThumbnailSynchronization();
    }

    private void ListBox_OnContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container is not ListBoxItem lbi)
            return;

        lbi.Tapped -= WorkItem_OnTapped;
        lbi.DoubleTapped -= WorkItem_OnDoubleTapped;
        lbi.Loaded -= ListBoxItem_OnLoaded;
        lbi.Unloaded -= ListBoxItem_OnUnloaded;

        _ = TrackedContainers.Remove(lbi);

        if (lbi.DataContext is IWorkViewModel vm)
        {
            _ = ViewportRetainedEntries.Remove(vm);
            vm.UnloadThumbnail(ThumbnailReferenceKey);
        }

        ScheduleViewportThumbnailSynchronization();
    }

    private void ListBoxItem_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is ListBoxItem lbi)
        {
            _ = TrackedContainers.Add(lbi);
            ScheduleViewportThumbnailSynchronization();
        }
    }

    private void ListBoxItem_OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is ListBoxItem lbi)
            _ = TrackedContainers.Remove(lbi);
    }

    private void EnsureScrollViewerAttached()
    {
        if (ListBox.Scroll is not ScrollViewer scrollViewer)
        {
            Dispatcher.UIThread.Post(EnsureScrollViewerAttached, DispatcherPriority.Loaded);
            return;
        }

        if (ReferenceEquals(_scrollViewer, scrollViewer))
            return;

        DetachScrollViewer();

        _scrollViewer = scrollViewer;
        _scrollViewer.ScrollChanged += ScrollViewer_OnScrollChanged;
        _scrollViewer.PropertyChanged += ScrollViewer_OnPropertyChanged;
    }

    private void DetachScrollViewer()
    {
        if (_scrollViewer is null)
            return;

        _scrollViewer.ScrollChanged -= ScrollViewer_OnScrollChanged;
        _scrollViewer.PropertyChanged -= ScrollViewer_OnPropertyChanged;
        _scrollViewer = null;
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            _savedOffset = scrollViewer.Offset;
            _hasSavedOffset = true;
        }

        ScheduleViewportThumbnailSynchronization();
    }

    private void ScrollViewer_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty || e.Property == ScrollViewer.ExtentProperty)
        {
            ScheduleRestoreOffset();
            ScheduleViewportThumbnailSynchronization();
        }
    }

    private void ScheduleRestoreOffset()
    {
        if (!_hasSavedOffset || _restoreOffsetScheduled)
            return;

        _restoreOffsetScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _restoreOffsetScheduled = false;
            RestoreOffsetIfNeeded();
        }, DispatcherPriority.Loaded);
    }

    private void RestoreOffsetIfNeeded()
    {
        if (!_hasSavedOffset)
            return;

        EnsureScrollViewerAttached();
        if (_scrollViewer is not { } scrollViewer)
            return;

        var viewportHeight = scrollViewer.Viewport.Height;
        var extentHeight = scrollViewer.Extent.Height;
        if (viewportHeight <= 0 || extentHeight <= 0)
        {
            ScheduleRestoreOffset();
            return;
        }

        var maxOffsetY = Math.Max(0, extentHeight - viewportHeight);
        var maxOffsetX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        var offset = new Vector(
            Math.Clamp(_savedOffset.X, 0, maxOffsetX),
            Math.Clamp(_savedOffset.Y, 0, maxOffsetY));

        scrollViewer.Offset = offset;
        _savedOffset = offset;
    }

    private void ScheduleViewportThumbnailSynchronization()
    {
        if (_syncScheduled)
            return;

        _syncScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _syncScheduled = false;
            _ = SynchronizeViewportThumbnailsAsync();
        }, DispatcherPriority.Background);
    }

    private async Task SynchronizeViewportThumbnailsAsync()
    {
        EnsureScrollViewerAttached();

        if (_scrollViewer is not { } scrollViewer)
            return;

        var viewportHeight = scrollViewer.Viewport.Height;
        if (viewportHeight <= 0)
            return;

        var offsetTop = scrollViewer.Offset.Y;
        var preload = Math.Max(ViewportRecyclePreloadMin, viewportHeight * ViewportRecyclePreloadFactor);
        var activeTop = Math.Max(0, offsetTop - preload);
        var activeBottom = offsetTop + viewportHeight + preload;

        var activeEntries = new HashSet<IWorkViewModel>();
        var pendingLoads = new List<(ListBoxItem Container, IWorkViewModel ViewModel)>();

        foreach (var container in TrackedContainers.ToArray())
        {
            if (!container.IsAttachedToVisualTree())
            {
                _ = TrackedContainers.Remove(container);
                continue;
            }

            if (container.DataContext is not IWorkViewModel vm)
                continue;

            var bounds = container.Bounds;
            if (bounds.Bottom < activeTop || bounds.Top > activeBottom)
                continue;

            if (!activeEntries.Add(vm))
                continue;

            if (!ViewportRetainedEntries.Contains(vm))
                pendingLoads.Add((container, vm));
        }

        foreach (var vm in ViewportRetainedEntries.Where(vm => !activeEntries.Contains(vm)).ToArray())
        {
            vm.UnloadThumbnail(ThumbnailReferenceKey);
            _ = ViewportRetainedEntries.Remove(vm);
        }

        if (pendingLoads.Count is 0)
            return;

        foreach (var (_, vm) in pendingLoads)
            _ = ViewportRetainedEntries.Add(vm);

        await Task.WhenAll(pendingLoads.Select(t => LoadAndAnimateAsync(t.Container, t.ViewModel)));
    }

    private async Task LoadAndAnimateAsync(ListBoxItem container, IWorkViewModel viewModel)
    {
        if (!await viewModel.TryLoadThumbnailAsync(ThumbnailReferenceKey))
        {
            _ = ViewportRetainedEntries.Remove(viewModel);
            ScheduleThumbnailRetry();
            return;
        }

        if (!container.IsAttachedToVisualTree())
            return;

        if (container.GetVisualDescendants().OfType<IWorkAnimatable>().FirstOrDefault() is { } animatable)
            animatable.StartAnimation();
    }

    private void ScheduleThumbnailRetry()
    {
        if (_retryScheduled)
            return;

        _retryScheduled = true;
        _ = Task.Run(async () =>
        {
            await Task.Delay(ThumbnailRetryDelay).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                _retryScheduled = false;
                ScheduleViewportThumbnailSynchronization();
            }, DispatcherPriority.Background);
        });
    }

    private void ReleaseAllRetainedViewportThumbnails()
    {
        foreach (var vm in ViewportRetainedEntries)
            vm.UnloadThumbnail(ThumbnailReferenceKey);

        ViewportRetainedEntries.Clear();
        TrackedContainers.Clear();
    }
}
