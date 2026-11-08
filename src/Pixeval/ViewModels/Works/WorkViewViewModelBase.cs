// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Config;
using Pixeval.Common.Collections;
using Pixeval.Mako;

namespace Pixeval.ViewModels.Works;

public abstract partial class WorkViewViewModelBase<T, TViewModel>(AppSettings appSettings, IBlockedContentService? blockedContent = null)
    : EntryViewViewModel<T, TViewModel>(blockedContent), IWorkViewViewModel
    where T : notnull
    where TViewModel : notnull
{
    public FrozenSet<string> CachedBlockedTags { get; private set; } = appSettings.BrowsingExperienceSettings.BlockedTags.ToFrozenSet();

    [ObservableProperty]
    public partial bool IsSelecting { get; set; }

    public AvaloniaList<object> SelectedEntries { get; } = [];

    private FilteredIncrementalCollection<TViewModel>? _filteredView;
    private LocalSortOption _sortOption = LocalSortOption.DoNotSort;
    private Predicate<object>? _userFilter;

    public override ObservableCollection<TViewModel> View =>
        _filteredView ??= CreateFilteredView();

    public void ReplaceBlockedEntries(IBlockedContentService blockedContent)
    {
        var snapshot = blockedContent.CaptureSnapshot();
        for (var index = 0; index < Source.Count; index++)
            Source[index] = BlockedContentHelper.ReplaceEntry(Source[index], snapshot);
        SelectedEntries.Clear();
    }

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

    IReadOnlyCollection<object> IOperableViewViewModel.View => ((ISimpleViewViewModel) this).View;

    IReadOnlyCollection<object> IOperableViewViewModel.Source => ((ISimpleViewViewModel) this).Source;


    public abstract bool RequireAdaptiveGrid { get; }

    public override void ResetSource(IAsyncEnumerable<T>? source, Func<T, int, TViewModel>? factory = null, int itemsPerPage = 20, int itemLimit = -1)
    {
        base.ResetSource(source, factory, itemsPerPage, itemLimit);
        _filteredView?.Dispose();
        _filteredView = CreateFilteredView();
        OnPropertyChanged(nameof(View));
    }

    public override void ShareSourceFrom(EntryViewViewModel<T, TViewModel> owner)
    {
        _filteredView?.Dispose();
        _filteredView = null;
        base.ShareSourceFrom(owner);
        _filteredView = CreateFilteredView();
        OnPropertyChanged(nameof(View));
    }

    public void ResetSource(IAsyncEnumerable<object>? source, int itemsPerPage = 20, int itemLimit = -1)
    {
        CachedBlockedTags = [.. appSettings.BrowsingExperienceSettings.BlockedTags.ToFrozenSet()];
        var typed = source as IAsyncEnumerable<T> ?? (source is IPageFeed<object> feed
            ? new TypedPageFeed(source, feed)
            : source is not null ? CastSource(source) : null);
        ResetSource(typed, static (info, _) => (TViewModel) (object) info, itemsPerPage, itemLimit);
    }

    private static async IAsyncEnumerable<T> CastSource(IAsyncEnumerable<object> items, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var item in items.WithCancellation(token))
            yield return (T) item;
    }

    private sealed class TypedPageFeed(IAsyncEnumerable<object> source, IPageFeed<object> feed) : IAsyncEnumerable<T>, IPageFeed<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token = default) => CastSource(source, token).GetAsyncEnumerator(token);
        public void Cancel() => feed.Cancel();
        public async Task<(IReadOnlyList<T> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
        {
            var (items, exhausted) = await feed.FetchPageAsync(cancellationToken).ConfigureAwait(false);
            return (items.Cast<T>().ToArray(), exhausted);
        }
    }

    private FilteredIncrementalCollection<TViewModel> CreateFilteredView()
    {
        return new FilteredIncrementalCollection<TViewModel>((IncrementalLoadingCollection<TViewModel>) Source, ownsSource: false)
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
