// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Collections;
using Pixeval.Models.Options;
using Pixeval.Native.Mako;

namespace Pixeval.ViewModels;

public abstract partial class WorkViewViewModelBase<T, TViewModel>(FrozenSet<string>? blockedTags)
    : EntryViewViewModel<T, TViewModel>, IWorkViewViewModel
    where T : class
    where TViewModel : class
{
    public FrozenSet<string> CachedBlockedTags { get; private set; } = blockedTags ?? App.AppViewModel.AppSettings.BrowsingExperienceSettings.BlockedTags.ToFrozenSet();

    [ObservableProperty]
    public partial bool IsSelecting { get; set; }

    public AvaloniaList<object> SelectedEntries { get; } = [];

    private FilteredIncrementalCollection<TViewModel>? _filteredView;
    private LocalSortOption _sortOption = LocalSortOption.DoNotSort;
    private Predicate<object>? _userFilter;

    public override ObservableCollection<TViewModel> View =>
        _filteredView ??= CreateFilteredView();

    public void SetSortOption(LocalSortOption sortOption)
    {
        if (_sortOption == sortOption)
            return;

        _sortOption = sortOption;
        if (_filteredView is not null)
            _filteredView.Comparer = ArtworkInfoExtensions.GetComparer(_sortOption);
    }

    public Predicate<object>? UserFilter
    {
        get => _userFilter;
        set
        {
            if (Equals(_userFilter, value))
                return;

            _userFilter = value;
            if (_filteredView is not null)
                _filteredView.Filter = value;
        }
    }

    IReadOnlyCollection<object> IOperableViewViewModel.View => View;

    IReadOnlyCollection<object> IOperableViewViewModel.Source => Source;

    public abstract bool RequireAdaptiveGrid { get; }

    public override void ResetEngine(IAsyncEnumerable<T>? newEngine, Func<T, int, TViewModel>? factory = null, int itemsPerPage = 20, int itemLimit = -1)
    {
        base.ResetEngine(newEngine, factory, itemsPerPage, itemLimit);
        _filteredView?.Dispose();
        _filteredView = CreateFilteredView();
        OnPropertyChanged(nameof(View));
    }

    public void ResetEngine(IAsyncEnumerable<object>? newEngine, int itemsPerPage = 20, int itemLimit = -1)
    {
        CachedBlockedTags = [.. App.AppViewModel.AppSettings.BrowsingExperienceSettings.BlockedTags.ToFrozenSet()];
        var typedEngine = newEngine as IAsyncEnumerable<T> ?? (newEngine is not null ? CastEngine(newEngine) : null);
        ResetEngine(typedEngine, static (info, _) => (TViewModel) (object) info, itemsPerPage, itemLimit);

        static async IAsyncEnumerable<T> CastEngine(IAsyncEnumerable<object> source)
        {
            await foreach (var item in source)
                yield return (T) item;
        }
    }

    private FilteredIncrementalCollection<TViewModel> CreateFilteredView()
    {
        return new FilteredIncrementalCollection<TViewModel>((IncrementalLoadingCollection<TViewModel>) Source)
        {
            Comparer = ArtworkInfoExtensions.GetComparer(_sortOption),
            Filter = _userFilter
        };
    }

    public override void Dispose()
    {
        base.Dispose();
        _filteredView?.Dispose();
        _filteredView = null;
    }
}
