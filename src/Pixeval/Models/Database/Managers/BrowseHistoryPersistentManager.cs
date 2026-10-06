// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.Models.Database.Managers;

public sealed class BrowseHistoryPersistentManager : WorkHistoryPersistentManager<BrowseHistoryEntry>
{
    public BrowseHistoryPersistentManager(StorageEngine storage, FileLogger logger) : base(storage, logger)
    {
    }

    public override int Count => (int)Storage.CountBrowseHistory();

    public override BrowseHistoryEntry? GetByWorkKey(string workKey)
    {
        if (string.IsNullOrWhiteSpace(workKey))
            return null;

        var r = Storage.GetBrowseHistoryByWorkKey(workKey);
        if (r is null || r.PayloadJson is null)
            return null;

        try
        {
            var entry = new BrowseHistoryEntry
            {
                HistoryEntryId = (int)r.HistoryEntryId,
                Id = r.Id,
                SerializeKey = r.SerializeKey,
                WorkKey = r.WorkKey
            };
            entry.AttachPayload(new ArtworkPayloadEntry { SerializedArtwork = r.PayloadJson });
            return entry;
        }
        catch
        {
            return null;
        }
    }

    public override void AddOrReplace(BrowseHistoryEntry entry)
    {
        var payloadJson = entry.Payload?.SerializedArtwork ?? (entry.Entry as Misaki.ISerializable)?.Serialize() ?? "";
        var r = Storage.AddOrReplaceBrowseHistory(entry.Id, entry.SerializeKey, entry.WorkKey, payloadJson);
        entry.HistoryEntryId = (int)r.HistoryEntryId;
        OnChanged();
    }

    public override bool TryDeleteByWorkKey(string workKey)
    {
        if (string.IsNullOrWhiteSpace(workKey))
            return false;

        var ok = Storage.TryDeleteBrowseHistoryByWorkKey(workKey);
        if (ok)
            OnChanged();
        return ok;
    }

    public override void Clear()
    {
        Storage.ClearBrowseHistory();
        OnChanged();
    }

    public override async IAsyncEnumerable<BrowseHistoryEntry> StreamEntriesAsync(
        int skip = 0,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var currentSkip = (uint)skip;
        const uint pageSize = 100;
        while (!token.IsCancellationRequested)
        {
            var records = Storage.StreamBrowseHistory(currentSkip, pageSize);
            if (records.Count == 0)
                yield break;

            foreach (var r in records)
            {
                token.ThrowIfCancellationRequested();
                if (r.PayloadJson is not null && TryHydrateBrowseEntry(r, out var entry))
                {
                    yield return entry;
                }
            }

            if (records.Count < pageSize)
                yield break;

            currentSkip += (uint)records.Count;
        }
    }

    private static bool TryHydrateBrowseEntry(BrowseHistoryRecord r, out BrowseHistoryEntry entry)
    {
        entry = new BrowseHistoryEntry
        {
            HistoryEntryId = (int)r.HistoryEntryId,
            Id = r.Id,
            SerializeKey = r.SerializeKey ?? "",
            WorkKey = r.WorkKey
        };
        try
        {
            entry.AttachPayload(new ArtworkPayloadEntry { SerializedArtwork = r.PayloadJson! });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
