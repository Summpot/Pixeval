// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Models.Blocking;
using Pixeval.Models.Options;
using Pixeval.Native.Mako;

namespace Pixeval.ViewModels;

public sealed partial class SimpleOperableViewViewModel<TViewModel> : ViewModelBase, IOperableViewViewModel, IDisposable
    where TViewModel : class
{
    private bool _isDisposed;
    private LocalSortOption _sortOption = LocalSortOption.DoNotSort;
    private Predicate<object>? _userFilter;

    public SimpleOperableViewViewModel(IReadOnlyCollection<object> source, bool needRefreshOnOpen = false)
    {
        NeedRefreshOnOpen = needRefreshOnOpen;
        Source = [.. source.Select(static entry => BlockedContentHelper.Replace(entry))];
        RefreshView();
    }

    public ObservableCollection<object> Source { get; }

    public ObservableCollection<object> View { get; } = [];

    public bool NeedRefreshOnOpen { get; }

    /// <inheritdoc />
    [ObservableProperty]
    public partial bool IsSelecting { get; set; }

    /// <inheritdoc />
    public AvaloniaList<object> SelectedEntries { get; } = [];

    public void SetSortOption(LocalSortOption sortOption)
    {
        if (_sortOption == sortOption)
            return;

        _sortOption = sortOption;
        RefreshView();
    }

    public Predicate<object>? UserFilter
    {
        get => _userFilter;
        set
        {
            if (Equals(_userFilter, value))
                return;

            _userFilter = value;
            RefreshView();
        }
    }

    private void RefreshView()
    {
        View.Clear();
        var items = Source.Where(entry => entry is TViewModel);
        if (_userFilter is not null)
            items = items.Where(entry => _userFilter(entry));
        if (_sortOption is not LocalSortOption.DoNotSort)
        {
            var comparer = ArtworkInfoExtensions.GetComparer(_sortOption);
            if (comparer is not null)
                items = items.OrderBy(entry => entry, comparer);
        }

        foreach (var item in items)
            View.Add(item);
    }

    /// <inheritdoc />
    IReadOnlyCollection<object> IOperableViewViewModel.View => View;

    /// <inheritdoc />
    public IReadOnlyCollection<object> SourceItems => Source;

    IReadOnlyCollection<object> IOperableViewViewModel.Source => Source;

    /// <inheritdoc />
    public bool RequireAdaptiveGrid => typeof(TViewModel) == typeof(Novel);

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        View.Clear();
        Source.Clear();
    }
}
