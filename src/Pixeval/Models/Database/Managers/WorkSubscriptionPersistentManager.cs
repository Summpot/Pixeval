// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Models.Options;
using Pixeval.Native.Storage;

namespace Pixeval.Models.Database.Managers;

public sealed class WorkSubscriptionPersistentManager : SqlitePersistentManager
{
    public WorkSubscriptionPersistentManager(StorageEngine storage) : base(storage)
    {
    }

    public int Count => (int)Storage.CountSubscriptions();

    public WorkSubscriptionRecord? GetBySubscriptionKey(
        long targetId,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind)
    {
        return Storage.GetSubscriptionByKey(targetId, (uint)subscriptionType, (uint)workKind);
    }

    public WorkSubscriptionRecord? GetByKey(long key)
    {
        if (key <= 0)
            return null;

        return Storage.GetSubscriptionByHistoryId(key);
    }

    public void Insert(WorkSubscriptionRecord entry) => Upsert(entry);

    public void AddOrUpdate(WorkSubscriptionRecord entry) => Upsert(entry);

    public void Update(WorkSubscriptionRecord entry) => Upsert(entry);

    public WorkSubscriptionRecord Upsert(WorkSubscriptionRecord entry)
    {
        return Storage.UpsertSubscription(
            entry.Id,
            entry.SubscriptionType,
            entry.WorkKind,
            entry.Title,
            entry.Author,
            entry.Avatar,
            entry.LastCheckTime,
            entry.LastWorkId);
    }

    public bool TryDelete(WorkSubscriptionRecord item)
    {
        return Storage.DeleteSubscription(item.HistoryEntryId);
    }

    public bool TryDeleteByHistoryEntryId(long historyEntryId)
    {
        return Storage.DeleteSubscription(historyEntryId);
    }

    internal IReadOnlySet<int> GetHistoryEntryIds()
    {
        return Storage.GetAllSubscriptionHistoryIds().Select(static id => (int)id).ToHashSet();
    }

    public void Clear()
    {
        Storage.ClearSubscriptions();
    }

    public async IAsyncEnumerable<WorkSubscriptionRecord> StreamEntriesAsync(
        int skip = 0,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var currentSkip = (uint)skip;
        const uint pageSize = 100;
        while (!token.IsCancellationRequested)
        {
            var records = Storage.StreamSubscriptions(currentSkip, pageSize);
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
