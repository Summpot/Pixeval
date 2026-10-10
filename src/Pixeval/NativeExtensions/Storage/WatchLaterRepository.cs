// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;

namespace Pixeval.Native.Storage;

public partial class WatchLaterRepository : IArtworkHistorySource
{
    public event EventHandler? Changed;

    internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Clear() => ClearWatchLater();

    public bool ContainsWatchLater(object entry) =>
        WatchLaterRecord.TryCreateWorkKey(entry, out var workKey) && ContainsWatchLater(workKey);

    public bool AddWatchLater(object entry)
    {
        if (!WatchLaterRecord.TryCreateWorkKey(entry, out var workKey))
            return false;
        var serializable = entry as IArtworkSerializable;
        var serializeKey = serializable?.SerializeKey;
        var payloadJson = serializable?.Serialize();
        var id = entry switch
        {
            Illustration i => i.Id.ToString(),
            Pixeval.Native.Mako.Novel n => n.Id.ToString(),
            Booru.BooruPost b => b.Id,
            SauceNao.SauceNaoItem s => s.RawId,
            _ => ""
        };
        AddOrReplaceWatchLater(id, serializeKey, workKey, payloadJson ?? "");
        return true;
    }

    public bool RemoveWatchLater(object entry) =>
        WatchLaterRecord.TryCreateWorkKey(entry, out var workKey) && RemoveWatchLater(workKey);

    public async IAsyncEnumerable<object> StreamAsync(SimpleWorkType workType, [EnumeratorCancellation] CancellationToken token = default)
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
}
