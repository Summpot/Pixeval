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
    private readonly ObservableCollection<DownloadItemViewModel> _source;

    private readonly List<DownloadItemViewModel> _filteredTasks = [];

    private bool _isDisposed;

    [ObservableProperty]
    public partial DownloadListOption CurrentOption { get; set; } = DownloadListOption.AllQueued;

    [ObservableProperty]
    public partial string? FilterText { get; set; }

    public DownloadPageViewModel PageViewModel { get; }

    public DownloadFolderViewModel? Folder { get; }

    public ObservableCollection<DownloadItemViewModel> View { get; } = [];

    partial void OnCurrentOptionChanged(DownloadListOption value) => RefreshView();

    partial void OnFilterTextChanged(string? value) => UpdateFilteredTasks(value);

    public DownloadItemPageViewModel(DownloadPageViewModel pageViewModel, DownloadFolderViewModel? folder = null)
    {
        PageViewModel = pageViewModel;
        Folder = folder;
        _source = folder?.Items ?? pageViewModel.OrdinaryItems;
        _source.CollectionChanged += OnSourceCollectionChanged;
        RefreshView();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshView();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        _source.CollectionChanged -= OnSourceCollectionChanged;
        View.Clear();
        _filteredTasks.Clear();
    }

    private void RefreshView()
    {
        if (_isDisposed)
            return;

        var filterSource = GetCustomSearchResult()?.ToHashSet();
        var matches = _source.Where(item => item.MatchesOption(CurrentOption, filterSource)).ToList();

        View.Clear();
        foreach (var item in matches)
        {
            View.Add(item);
        }
    }

    private IReadOnlyCollection<DownloadItemViewModel>? GetCustomSearchResult() =>
        CurrentOption is DownloadListOption.CustomSearch && !string.IsNullOrWhiteSpace(FilterText)
            ? _filteredTasks
            : null;

    private void UpdateFilteredTasks(string? key)
    {
        _filteredTasks.Clear();
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var item in _source.Where(item => item.MatchesSearch(key)))
            {
                _filteredTasks.Add(item);
            }
        }

        RefreshView();
    }
}
