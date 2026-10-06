// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Misaki;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Storage;

public partial class WatchLaterRepository : IArtworkHistorySource
{
    public event EventHandler? Changed;

    internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Clear() => ClearWatchLater();

    public bool ContainsWatchLater(IArtworkInfo entry) =>
        WatchLaterRecord.TryCreateWorkKey(entry, out var workKey) && Contains(workKey);

    public bool AddWatchLater(IArtworkInfo entry)
    {
        if (!WatchLaterRecord.TryCreateWorkKey(entry, out var workKey))
            return false;
        var serializable = entry as ISerializable;
        var serializeKey = serializable?.SerializeKey;
        var payloadJson = serializable?.Serialize();
        AddOrReplaceWatchLater(entry.Id.ToString(), serializeKey, workKey, payloadJson ?? "");
        return true;
    }

    public bool RemoveWatchLater(IArtworkInfo entry) =>
        WatchLaterRecord.TryCreateWorkKey(entry, out var workKey) && Remove(workKey);

    public async IAsyncEnumerable<IArtworkInfo> StreamAsync(SimpleWorkType workType, [EnumeratorCancellation] CancellationToken token = default)
    {
        long? cursorId = null;
        const int pageSize = 50;

        while (!token.IsCancellationRequested)
        {
            var batch = StreamWatchLaterCursor(cursorId, (uint)pageSize);
            if (batch.Count == 0)
                yield break;

            foreach (var record in batch)
            {
                token.ThrowIfCancellationRequested();
                if (record.Entry is { } entry && HistoryRepository.MatchesWorkType(workType, record.SerializeKey, entry))
                {
                    yield return entry;
                }
            }

            cursorId = batch[^1].HistoryEntryId;
            if (batch.Count < pageSize)
                yield break;
        }
    }

    public bool Contains(string workKey) => ContainsWatchLater(workKey);

    public bool Remove(string workKey) => RemoveWatchLater(workKey);
}
