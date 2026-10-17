// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Pixeval.Common.Collections;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that can append a page of items with one change notification.
/// </summary>
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    private static readonly PropertyChangedEventArgs CountChanged = new(nameof(Count));
    private static readonly PropertyChangedEventArgs IndexerChanged = new("Item[]");

    /// <summary>
    /// Appends <paramref name="items"/> and raises a single <see cref="NotifyCollectionChangedAction.Add"/>.
    /// </summary>
    public void AddRange(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        switch (items.Count)
        {
            case 0:
                return;
            case 1:
                Add(items[0]);
                return;
        }

        CheckReentrancy();
        var startIndex = Count;
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(CountChanged);
        OnPropertyChanged(IndexerChanged);
        IList changedItems = items as IList ?? items.ToList();
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add,
            changedItems,
            startIndex));
    }
}
