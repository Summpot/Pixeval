// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Collections;
using Pixeval.Download;
using Pixeval.I18N;
using Pixeval.Models.Database.Managers;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Download;

public sealed partial class DownloadPageViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollectionAdapter<IDownloadTaskGroupBase, DownloadItemViewModel> _sourceAdapter;

    private readonly HashSet<DownloadItemViewModel> _trackedItems = [];

    public DownloadPageViewModel(ObservableCollection<IDownloadTaskGroupBase> source)
    {
        _sourceAdapter = new ObservableCollectionAdapter<IDownloadTaskGroupBase, DownloadItemViewModel>(source);
        View = new AdvancedObservableCollection<DownloadItemViewModel>(_sourceAdapter, true);
        View.ObserveFilterProperty(nameof(DownloadItemViewModel.CurrentState));
        View.CollectionChanged += ViewOnCollectionChanged;
        _sourceAdapter.CollectionChanged += SourceAdapterOnCollectionChanged;

        FilterOptions =
        [
            new DownloadFilterOptionItem(DownloadListOption.AllQueued, I18NManager.GetResource(DownloadPageResources.DownloadListOptionAllQueued)),
            new DownloadFilterOptionItem(DownloadListOption.Running, I18NManager.GetResource(DownloadPageResources.DownloadListOptionRunning)),
            new DownloadFilterOptionItem(DownloadListOption.Completed, I18NManager.GetResource(DownloadPageResources.DownloadListOptionCompleted)),
            new DownloadFilterOptionItem(DownloadListOption.Cancelled, I18NManager.GetResource(DownloadPageResources.DownloadListOptionCancelled)),
            new DownloadFilterOptionItem(DownloadListOption.Error, I18NManager.GetResource(DownloadPageResources.DownloadListOptionError))
        ];

        SelectionLabel = I18NManager.GetResource(DownloadPageResources.CancelSelectionButtonDefaultLabel);
        SelectedFilter = FilterOptions[0];

        RewireItemSubscriptions();
        ResetFilter();
        RefreshSummary();
    }

    public AdvancedObservableCollection<DownloadItemViewModel> View { get; }

    public IReadOnlyList<DownloadFilterOptionItem> FilterOptions { get; }

    [ObservableProperty]
    public partial DownloadFilterOptionItem? SelectedFilter { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAnyEntrySelected { get; set; }

    [ObservableProperty]
    public partial string SelectionLabel { get; set; }

    [ObservableProperty]
    public partial int TotalCount { get; private set; }

    [ObservableProperty]
    public partial int VisibleCount { get; private set; }

    [ObservableProperty]
    public partial int ActiveCount { get; private set; }

    [ObservableProperty]
    public partial int CompletedCount { get; private set; }

    [ObservableProperty]
    public partial int FailedCount { get; private set; }

    public bool HasNoItem => VisibleCount is 0;

    public string EmptyStateText => TotalCount is 0
        ? "还没有下载任务，先去收藏几张图吧。"
        : "没有符合当前筛选条件的下载任务。";

    public string VisibleSummaryText => string.IsNullOrWhiteSpace(SearchText)
        ? $"共 {VisibleCount} 项"
        : $"显示 {VisibleCount} / {TotalCount} 项";

    public DownloadItemViewModel[] SelectedEntries
    {
        get;
        set
        {
            if (Equals(value, field))
                return;

            field = value;
            var count = value.Length;
            IsAnyEntrySelected = count > 0;
            SelectionLabel = IsAnyEntrySelected
                ? I18NManager.GetResource(DownloadPageResources.CancelSelectionButtonFormatted, count)
                : I18NManager.GetResource(DownloadPageResources.CancelSelectionButtonDefaultLabel);
            OnPropertyChanged();
        }
    } = [];

    partial void OnSelectedFilterChanged(DownloadFilterOptionItem? value)
    {
        _ = value;
        ResetFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = value;
        ResetFilter();
    }

    public void PauseSelectedItems()
    {
        foreach (var item in SelectedEntries)
            item.DownloadTask.Pause();
    }

    public void ResumeSelectedItems()
    {
        foreach (var item in SelectedEntries)
            item.DownloadTask.TryResume();
    }

    public void CancelSelectedItems()
    {
        foreach (var item in SelectedEntries)
            item.DownloadTask.Cancel();
    }

    public int RemoveSelectedItems(bool deleteLocalFiles)
    {
        var count = SelectedEntries.Length;
        if (count is 0)
            return 0;

        var manager = App.AppViewModel.AppServiceProvider.GetRequiredService<DownloadHistoryPersistentManager>();
        foreach (var item in SelectedEntries)
        {
            if (deleteLocalFiles)
                item.DownloadTask.Delete();

            App.AppViewModel.DownloadManager.RemoveTask(item.DownloadTask);
            _ = manager.Delete(entry => entry.Destination == item.DownloadTask.Destination);
        }

        SelectedEntries = [];
        RefreshSummary();
        return count;
    }

    public void ResetFilter()
    {
        View.Filter = item => MatchesState(item) && MatchesSearch(item);
        RefreshSummary();
    }

    public void Dispose()
    {
        View.CollectionChanged -= ViewOnCollectionChanged;
        _sourceAdapter.CollectionChanged -= SourceAdapterOnCollectionChanged;

        foreach (var item in _trackedItems)
            item.PropertyChanged -= DownloadItemOnPropertyChanged;

        _trackedItems.Clear();

        foreach (var item in _sourceAdapter)
            item.Dispose();

        View.Dispose();
    }

    private bool MatchesState(DownloadItemViewModel item)
    {
        return (SelectedFilter?.Option ?? DownloadListOption.AllQueued) switch
        {
            DownloadListOption.AllQueued => true,
            DownloadListOption.Running => item.CurrentState is DownloadState.Running or DownloadState.Queued or DownloadState.Pending or DownloadState.Paused,
            DownloadListOption.Completed => item.CurrentState is DownloadState.Completed,
            DownloadListOption.Cancelled => item.CurrentState is DownloadState.Cancelled,
            DownloadListOption.Error => item.CurrentState is DownloadState.Error,
            DownloadListOption.CustomSearch => true,
            _ => throw new ArgumentOutOfRangeException(nameof(SelectedFilter), SelectedFilter?.Option, null)
        };
    }

    private bool MatchesSearch(DownloadItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        return item.Title.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
               || item.Id.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
               || item.AuthorText.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private void RefreshSummary()
    {
        TotalCount = _sourceAdapter.Count;
        VisibleCount = View.Count;
        ActiveCount = _sourceAdapter.Count(static item => item.CurrentState is DownloadState.Queued or DownloadState.Running or DownloadState.Pending or DownloadState.Paused);
        CompletedCount = _sourceAdapter.Count(static item => item.CurrentState is DownloadState.Completed);
        FailedCount = _sourceAdapter.Count(static item => item.CurrentState is DownloadState.Error);

        OnPropertyChanged(nameof(HasNoItem));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(VisibleSummaryText));
    }

    private void ViewOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshSummary();
    }

    private void SourceAdapterOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RewireItemSubscriptions();
        RefreshSummary();
    }

    private void RewireItemSubscriptions()
    {
        var currentItems = _sourceAdapter.ToHashSet();

        foreach (var item in _trackedItems.Where(item => !currentItems.Contains(item)).ToArray())
        {
            item.PropertyChanged -= DownloadItemOnPropertyChanged;
            _ = _trackedItems.Remove(item);
        }

        foreach (var item in currentItems.Where(item => _trackedItems.Add(item)))
            item.PropertyChanged += DownloadItemOnPropertyChanged;
    }

    private void DownloadItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is null)
            return;

        switch (e.PropertyName)
        {
            case nameof(DownloadItemViewModel.CurrentState):
            case nameof(DownloadItemViewModel.ProgressPercentage):
                RefreshSummary();
                break;
        }
    }
}

public sealed record DownloadFilterOptionItem(DownloadListOption Option, string Label)
{
    public override string ToString() => Label;
}
