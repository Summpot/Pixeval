// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Models.Options;

namespace Pixeval.ViewModels;

public sealed partial class DownloadItemPageViewModel : ViewModelBase, IDisposable
{
    private readonly ObservableCollection<DownloadItemSnapshot>? _ordinaryItems;

    private readonly List<DownloadTaskKey> _filteredKeys = [];

    private bool _isDisposed;

    [ObservableProperty]
    public partial DownloadListOption CurrentOption { get; set; } = DownloadListOption.AllQueued;

    [ObservableProperty]
    public partial string? FilterText { get; set; }

    public DownloadPageViewModel PageViewModel { get; }

    public long? SubscriptionId { get; }

    public DownloadFolderSnapshot? Folder =>
        SubscriptionId is { } subscriptionId
            ? PageViewModel.Folders.FirstOrDefault(folder => folder.SubscriptionId == subscriptionId)
            : null;

    public ObservableCollection<DownloadItemSnapshot> View { get; } = [];

    public event Action? ViewRefreshStarting;

    public event Action? ViewRefreshCompleted;

    partial void OnCurrentOptionChanged(DownloadListOption value) => RefreshView();

    partial void OnFilterTextChanged(string? value) => UpdateFilteredTasks(value);

    public DownloadItemPageViewModel(DownloadPageViewModel pageViewModel, long? subscriptionId = null)
    {
        PageViewModel = pageViewModel;
        SubscriptionId = subscriptionId;
        if (subscriptionId is null)
        {
            _ordinaryItems = pageViewModel.OrdinaryItems;
            _ordinaryItems.CollectionChanged += OnSourceChanged;
        }
        else
        {
            pageViewModel.Folders.CollectionChanged += OnFoldersChanged;
        }

        RefreshView();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        if (_ordinaryItems is not null)
            _ordinaryItems.CollectionChanged -= OnSourceChanged;
        else
            PageViewModel.Folders.CollectionChanged -= OnFoldersChanged;
        View.Clear();
        _filteredKeys.Clear();
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshView();

    private void OnFoldersChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshView();

    private IReadOnlyList<DownloadItemSnapshot> Source =>
        SubscriptionId is null
            ? _ordinaryItems ?? []
            : Folder?.Items ?? [];

    private void RefreshView()
    {
        if (_isDisposed)
            return;

        var filterSource = GetCustomSearchResult();
        var desired = Source.Where(item => item.MatchesOption(CurrentOption, filterSource)).ToList();
        if (SnapshotListDiff.Matches(View, desired, static item => item.Key))
            return;

        ViewRefreshStarting?.Invoke();
        SnapshotListDiff.Apply(View, desired, static item => item.Key);
        ViewRefreshCompleted?.Invoke();
    }

    private HashSet<DownloadTaskKey>? GetCustomSearchResult() =>
        CurrentOption is DownloadListOption.CustomSearch && !string.IsNullOrWhiteSpace(FilterText)
            ? _filteredKeys.ToHashSet()
            : null;

    private void UpdateFilteredTasks(string? key)
    {
        _filteredKeys.Clear();
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var item in Source.Where(item => item.MatchesSearch(key)))
                _filteredKeys.Add(item.Key);
        }

        RefreshView();
    }
}
