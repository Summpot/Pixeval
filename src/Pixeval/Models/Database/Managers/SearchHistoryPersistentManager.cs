// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Native.Storage;

namespace Pixeval.Models.Database.Managers;

public class SearchHistoryPersistentManager : SqlitePersistentManager
{
    public SearchHistoryPersistentManager(StorageEngine storage) : base(storage)
    {
    }

    public int Count => (int)Storage.CountSearchHistory();

    public SearchHistoryRecord? GetByValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Storage.GetSearchHistoryByValue(value);
    }

    public bool TryDeleteByValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return Storage.TryDeleteSearchHistoryByValue(value);
    }

    public void Insert(SearchHistoryRecord entry) => Upsert(entry);

    public void AddOrUpdate(SearchHistoryRecord entry) => Upsert(entry);

    public SearchHistoryRecord Upsert(SearchHistoryRecord entry)
    {
        return Storage.UpsertSearchHistory(
            entry.Value,
            entry.TranslatedName,
            entry.Time);
    }

    public void Clear()
    {
        Storage.ClearSearchHistory();
    }

    public async IAsyncEnumerable<SearchHistoryRecord> StreamEntriesAsync(
        int skip = 0,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var currentSkip = (uint)skip;
        const uint pageSize = 100;
        while (!token.IsCancellationRequested)
        {
            var records = Storage.StreamSearchHistories(currentSkip, pageSize);
            if (records.Count == 0)
                yield break;

            foreach (var r in records)
            {
                token.ThrowIfCancellationRequested();
                yield return r;
            }

            if (records.Count < pageSize)
                yield break;

            currentSkip += (uint)records.Count;
        }
    }
}
