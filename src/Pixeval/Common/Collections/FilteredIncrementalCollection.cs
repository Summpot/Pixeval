// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pixeval.Common.Collections;

public class FilteredIncrementalCollection<T> : ObservableRangeCollection<T>, IIncrementalLoading, IDisposable
    where T : notnull
{
    private readonly IncrementalLoadingCollection<T> _source;
    private Predicate<object>? _filter;
    private IComparer<object>? _comparer;
    private bool _isDisposed;

    public FilteredIncrementalCollection(IncrementalLoadingCollection<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _source.CollectionChanged += OnSourceCollectionChanged;
        Rebuild();
    }

    public Predicate<object>? Filter
    {
        get => _filter;
        set
        {
            if (Equals(_filter, value))
                return;

            _filter = value;
            Rebuild();
        }
    }

    public IComparer<object>? Comparer
    {
        get => _comparer;
        set
        {
            if (Equals(_comparer, value))
                return;

            _comparer = value;
            Rebuild();
        }
    }

    public bool HasMoreItems => _source.HasMoreItems;

    public bool IsInterrupted => _source.IsInterrupted;

    public Task<int> LoadMoreItemsAsync(int count, CancellationToken token = default) =>
        _source.LoadMoreItemsAsync(count, token);

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isDisposed)
            return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                AppendAddedItems(e.NewItems);
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                foreach (T item in e.OldItems)
                    Remove(item);
                break;

            case NotifyCollectionChangedAction.Reset:
            default:
                Rebuild();
                break;
        }
    }

    private void AppendAddedItems(IList newItems)
    {
        if (_comparer is not null)
        {
            foreach (T item in newItems)
            {
                if (_filter is null || _filter(item))
                    InsertSorted(item);
            }

            return;
        }

        if (_filter is null)
        {
            AddRange(CopyItems(newItems));
            return;
        }

        List<T>? matches = null;
        foreach (T item in newItems)
        {
            if (!_filter(item))
                continue;
            matches ??= new List<T>(newItems.Count);
            matches.Add(item);
        }

        if (matches is not null)
            AddRange(matches);
    }

    private static IReadOnlyList<T> CopyItems(IList newItems)
    {
        if (newItems.Count is 1)
            return [(T) newItems[0]!];

        var copy = new T[newItems.Count];
        newItems.CopyTo(copy, 0);
        return copy;
    }

    private void InsertSorted(T item)
    {
        var low = 0;
        var high = Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var cmp = _comparer!.Compare(item, this[mid]);
            if (cmp == 0)
            {
                Insert(mid, item);
                return;
            }

            if (cmp < 0)
                high = mid - 1;
            else
                low = mid + 1;
        }

        Insert(low, item);
    }

    public void Rebuild()
    {
        if (_isDisposed)
            return;

        Clear();
        var items = _source.AsEnumerable();
        if (_filter is not null)
            items = items.Where(x => _filter(x));
        if (_comparer is not null)
            items = items.OrderBy(x => x, _comparer);

        foreach (var item in items)
            Add(item);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        _source.CollectionChanged -= OnSourceCollectionChanged;
        _source.Dispose();
        Clear();
    }
}
