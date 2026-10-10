// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.I18N;
using Pixeval.Models;
using Pixeval.Models.Options;
using Pixeval.Native.Download;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.ViewContainers;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Download;

public partial class DownloadItemView : ContentPage, IDisposable
{
    public static readonly DirectProperty<DownloadItemView, int> SelectedCountProperty =
        AvaloniaProperty.RegisterDirect<DownloadItemView, int>(
            nameof(SelectedCount),
            view => view.SelectedCount);

    public static readonly DirectProperty<DownloadItemView, object?> DownloadItemsSourceProperty =
        AvaloniaProperty.RegisterDirect<DownloadItemView, object?>(
            nameof(DownloadItemsSource),
            view => view.DownloadItemsSource,
            (view, value) => view.DownloadItemsSource = value);

    public static readonly DirectProperty<DownloadItemView, bool> HasNoDownloadItemProperty =
        AvaloniaProperty.RegisterDirect<DownloadItemView, bool>(
            nameof(HasNoDownloadItem),
            view => view.HasNoDownloadItem);

    public static readonly DirectProperty<DownloadItemView, bool> DeleteLocalFilesProperty =
        AvaloniaProperty.RegisterDirect<DownloadItemView, bool>(
            nameof(DeleteLocalFiles),
            view => view.DeleteLocalFiles,
            (view, value) => view.DeleteLocalFiles = value);

    private readonly HashSet<DownloadTaskKey> _selectedKeys = [];

    private INotifyPropertyChanged? _subscribedItemsSource;

    private DownloadItemPageViewModel? _itemPage;

    private bool _restoringSelection;

    private bool _isDisposed;

    public int SelectedCount
    {
        get;
        private set => SetAndRaise(SelectedCountProperty, ref field, value);
    }

    public object? DownloadItemsSource
    {
        get;
        private set => SetAndRaise(DownloadItemsSourceProperty, ref field, value);
    }

    public bool HasNoDownloadItem
    {
        get;
        private set => SetAndRaise(HasNoDownloadItemProperty, ref field, value);
    } = true;

    public bool DeleteLocalFiles
    {
        get;
        set => SetAndRaise(DeleteLocalFilesProperty, ref field, value);
    }

    public static FuncValueConverter<int, string> SelectionCountLabelConverter { get; } = new(count =>
        count > 0
            ? I18NManager.GetResource(DownloadPageResources.CancelSelectionButtonFormatted, count)
            : I18NManager.GetResource(DownloadPageResources.CancelSelectionButtonDefaultLabel));

    public DownloadItemView() => InitializeComponent();

    public DownloadItemView(DownloadItemPageViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    public void SelectAll() => ListBox.SelectAll();

    public void UnselectAll() => ListBox.UnselectAll();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_isDisposed)
            return;

        UpdateItemsSourceSubscription();
        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, this));
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_isDisposed)
            return;

        UpdateItemsSourceSubscription();
        UpdateFolderHeader();
    }

    private void DownloadItem_OnOpenIllustrationRequested(DownloadItem sender, DownloadItemSnapshot item)
    {
        if (TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        OpenWork(viewContainer, item);
    }

    private static void OpenWork(ViewContainerBase viewContainer, DownloadItemSnapshot item)
    {
        if (!Uri.TryCreate(item.AppUri, UriKind.Absolute, out var uri) || uri.Scheme != "pixeval")
            return;

        var id = uri.AbsolutePath.Trim('/');
        if (uri.Host.Equals("novel", StringComparison.OrdinalIgnoreCase) && long.TryParse(id, out var novelId))
        {
            viewContainer.CreateNovelPage(novelId);
            return;
        }

        var platform = uri.Host.Equals("illust", StringComparison.OrdinalIgnoreCase)
            ? PlatformConstants.Pixiv
            : uri.Host;
        if (!string.IsNullOrWhiteSpace(id))
            viewContainer.CreateIllustrationPage(id, platform);
    }

    private void FilterTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not DownloadItemPageViewModel vm
            || e.Key is not Key.Enter
            || sender is not TextBox { Text: var filterText })
            return;

        vm.FilterText = filterText;
        vm.CurrentOption = string.IsNullOrWhiteSpace(filterText)
            ? DownloadListOption.AllQueued
            : DownloadListOption.CustomSearch;
    }

    private void PauseAllButton_OnClicked(object? sender, RoutedEventArgs e) =>
        ExecuteForSelectedDownloadTasks(item => Page?.Pause(item.Key));

    private void ResumeAllButton_OnClicked(object? sender, RoutedEventArgs e) =>
        ExecuteForSelectedDownloadTasks(item => Page?.Resume(item.Key));

    private void CancelAllButton_OnClicked(object? sender, RoutedEventArgs e) =>
        ExecuteForSelectedDownloadTasks(item => Page?.Cancel(item.Key));

    private void ResetAllButton_OnClicked(object? sender, RoutedEventArgs e) =>
        ExecuteForSelectedDownloadTasks(item => Page?.Reset(item.Key));

    private void DeleteAllButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (Page is not { } page || SelectedCount == 0)
            return;

        var count = SelectedCount;
        foreach (var item in GetSelectedEntries())
            page.Remove(item.Key, DeleteLocalFiles);

        UnselectAll();
        _selectedKeys.Clear();
        TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(
            I18NManager.GetResource(DownloadPageResources.DeleteDownloadHistoryRecordsFormatted, count));
    }

    private void SelectAllButton_OnClicked(object? sender, RoutedEventArgs e) => SelectAll();

    private void InvertSelectionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        var autoScroll = ListBox.AutoScrollToSelectedItem;
        ListBox.AutoScrollToSelectedItem = false;
        try
        {
            using var operation = ListBox.Selection.BatchUpdate();
            for (var i = 0; i < ListBox.Items.Count; i++)
            {
                if (ListBox.Selection.IsSelected(i))
                    ListBox.Selection.Deselect(i);
                else
                    ListBox.Selection.Select(i);
            }
        }
        finally
        {
            ListBox.AutoScrollToSelectedItem = autoScroll;
        }
    }

    private void CancelSelectButton_OnClicked(object? sender, RoutedEventArgs e) => UnselectAll();

    private DownloadPageViewModel? Page =>
        (DataContext as DownloadItemPageViewModel)?.PageViewModel;

    private void UpdateFolderHeader()
    {
        if (DataContext is DownloadItemPageViewModel { Folder: { } folder })
            Header = folder.Title;
    }

    private void UpdateItemsSourceSubscription()
    {
        DetachItemPage();
        if (DataContext is not DownloadItemPageViewModel vm)
            return;

        _itemPage = vm;
        vm.ViewRefreshStarting += OnViewRefreshStarting;
        vm.ViewRefreshCompleted += OnViewRefreshCompleted;
        vm.PageViewModel.Folders.CollectionChanged += OnFoldersChanged;
        SetDownloadItemsSource(vm.View);
        UpdateFolderHeader();
    }

    private void OnFoldersChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateFolderHeader();

    private void OnViewRefreshStarting()
    {
        _restoringSelection = true;
        _selectedKeys.Clear();
        if (ListBox.SelectedItems is null)
            return;

        foreach (var item in ListBox.SelectedItems.OfType<DownloadItemSnapshot>())
            _selectedKeys.Add(item.Key);
    }

    private void OnViewRefreshCompleted()
    {
        try
        {
            var desired = ListBox.Items.OfType<DownloadItemSnapshot>()
                .Where(item => _selectedKeys.Contains(item.Key))
                .ToList();
            var current = ListBox.SelectedItems?.OfType<DownloadItemSnapshot>().ToList() ?? [];
            var alreadySelected = current.Count == desired.Count
                && current.All(item => _selectedKeys.Contains(item.Key));
            if (!alreadySelected)
            {
                ListBox.SelectedItems?.Clear();
                foreach (var item in desired)
                    ListBox.SelectedItems?.Add(item);
            }

            _selectedKeys.Clear();
            foreach (var item in desired)
                _selectedKeys.Add(item.Key);
            SelectedCount = desired.Count;
        }
        finally
        {
            _restoringSelection = false;
        }
    }

    private void ItemsSource_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ICollection.Count))
            UpdateHasNoDownloadItem();
    }

    private void UpdateHasNoDownloadItem() =>
        HasNoDownloadItem = DownloadItemsSource is not ICollection collection || collection.Count is 0;

    private void SetDownloadItemsSource(object source)
    {
        if (ReferenceEquals(DownloadItemsSource, source))
        {
            UpdateHasNoDownloadItem();
            return;
        }

        UnsubscribeFromItemsSource();
        DownloadItemsSource = source;
        _subscribedItemsSource = source as INotifyPropertyChanged;
        _subscribedItemsSource?.PropertyChanged += ItemsSource_OnPropertyChanged;
        UpdateHasNoDownloadItem();
    }

    private void UnsubscribeFromItemsSource()
    {
        if (_subscribedItemsSource is not null)
            _subscribedItemsSource.PropertyChanged -= ItemsSource_OnPropertyChanged;
        _subscribedItemsSource = null;
    }

    private void DetachItemPage()
    {
        if (_itemPage is null)
            return;

        _itemPage.ViewRefreshStarting -= OnViewRefreshStarting;
        _itemPage.ViewRefreshCompleted -= OnViewRefreshCompleted;
        _itemPage.PageViewModel.Folders.CollectionChanged -= OnFoldersChanged;
        _itemPage = null;
    }

    private IReadOnlyList<DownloadItemSnapshot> GetSelectedEntries() =>
        [.. ListBox.SelectedItems?.OfType<DownloadItemSnapshot>() ?? []];

    private void ExecuteForSelectedDownloadTasks(Action<DownloadItemSnapshot> action)
    {
        foreach (var item in GetSelectedEntries())
            action(item);
    }

    private void ListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_restoringSelection)
            return;

        SelectedCount = ListBox.SelectedItems?.OfType<DownloadItemSnapshot>().Count() ?? 0;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        UnsubscribeFromItemsSource();
        DetachItemPage();
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
    }
}
