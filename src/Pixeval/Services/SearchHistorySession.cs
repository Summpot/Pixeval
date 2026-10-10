// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Pixeval.Native.Storage;

namespace Pixeval.Services;

public sealed class SearchHistorySession(StorageEngine storage, ObservableCollection<SearchHistoryRecord> entries)
{
    public ObservableCollection<SearchHistoryRecord> Entries { get; } = entries;

    public void Add(string text, string? translatedName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var entry = storage.UpsertSearchHistory(text, translatedName, DateTimeOffset.UtcNow.ToString("O"));
        if (Entries.FirstOrDefault(e => e.Value == text) is { } existing)
            Entries.Remove(existing);
        Entries.Insert(0, entry);
    }

    public bool Remove(SearchHistoryRecord entry) => Entries.Remove(entry);

    public void Clear()
    {
        storage.ClearSearchHistory();
        Entries.Clear();
    }
}
