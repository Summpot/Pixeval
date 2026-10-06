// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.Models.Database.Managers;

public sealed class DownloadHistoryPersistentManager : DownloadHistoryPersistentManagerBase<DownloadHistoryEntry>
{
    public DownloadHistoryPersistentManager(StorageEngine storage, FileLogger logger) : base(storage, logger)
    {
    }

    public override int Count => (int)Storage.CountDownloadHistory();

    public void AddOrReplace(DownloadHistoryEntry entry)
    {
        var payloadJson = entry.Payload?.SerializedArtwork ?? (entry.Entry as Misaki.ISerializable)?.Serialize() ?? "";
        var r = Storage.AddOrReplaceDownloadHistory(
            entry.Entry?.Id ?? "",
            entry.SerializeKey,
            entry.Destination,
            (uint)entry.State,
            entry.FormatToken,
            entry.ErrorMessage,
            payloadJson);
        entry.HistoryEntryId = (int)r.HistoryEntryId;
        OnChanged();
    }

    public void Insert(DownloadHistoryEntry entry) => AddOrReplace(entry);

    public void Update(DownloadHistoryEntry entry) => AddOrReplace(entry);

    public bool TryDeleteByDestination(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
            return false;

        var ok = Storage.TryDeleteDownloadHistoryByDestination(destination);
        if (ok)
            OnChanged();
        return ok;
    }

    public override void Clear()
    {
        Storage.ClearDownloadHistory();
        OnChanged();
    }

    public override async IAsyncEnumerable<DownloadHistoryEntry> StreamEntriesAsync(
        int skip = 0,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var currentSkip = (uint)skip;
        const uint pageSize = 100;
        while (!token.IsCancellationRequested)
        {
            var records = Storage.StreamDownloadHistory(currentSkip, pageSize);
            if (records.Count == 0)
                yield break;

            foreach (var r in records)
            {
                token.ThrowIfCancellationRequested();
                if (r.PayloadJson is not null && TryHydrateDownloadEntry(r, out var entry))
                {
                    yield return entry;
                }
            }

            if (records.Count < pageSize)
                yield break;

            currentSkip += (uint)records.Count;
        }
    }

    private static bool TryHydrateDownloadEntry(DownloadHistoryRecord r, out DownloadHistoryEntry entry)
    {
        entry = new DownloadHistoryEntry
        {
            HistoryEntryId = (int)r.HistoryEntryId,
            Destination = r.Destination,
            SerializeKey = r.SerializeKey ?? "",
            State = (Pixeval.Native.Download.DownloadState)r.State,
            FormatToken = r.FormatToken,
            ErrorMessage = r.ErrorMessage
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
