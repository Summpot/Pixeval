// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.Models.Database.Managers;

public sealed class SubscriptionDownloadHistoryPersistentManager : DownloadHistoryPersistentManagerBase<SubscriptionDownloadHistoryEntry>
{
    public SubscriptionDownloadHistoryPersistentManager(
        StorageEngine storage,
        FileLogger logger) : base(storage, logger)
    {
    }

    public override int Count => (int)Storage.CountSubscriptionDownloadHistory();

    public void AddOrReplace(SubscriptionDownloadHistoryEntry entry)
    {
        var payloadJson = entry.Payload?.SerializedArtwork ?? (entry.Entry as Misaki.ISerializable)?.Serialize() ?? "";
        var r = Storage.AddOrReplaceSubscriptionDownloadHistory(
            entry.ArtworkId,
            entry.SerializeKey,
            entry.Destination,
            (uint)entry.State,
            entry.FormatToken,
            entry.ErrorMessage,
            entry.WorkSubscriptionId,
            entry.ArtworkId,
            payloadJson);
        entry.HistoryEntryId = (int)r.HistoryEntryId;
        OnChanged();
    }

    public void Insert(SubscriptionDownloadHistoryEntry entry) => AddOrReplace(entry);

    public void Update(SubscriptionDownloadHistoryEntry entry) => AddOrReplace(entry);

    public void AddOrReplaceRange(IReadOnlyCollection<SubscriptionDownloadHistoryEntry> entries)
    {
        var records = entries.Select(entry =>
        {
            var payloadJson = entry.Payload?.SerializedArtwork ?? (entry.Entry as Misaki.ISerializable)?.Serialize() ?? "";
            return new SubscriptionDownloadHistoryRecord(
                entry.HistoryEntryId,
                entry.ArtworkId,
                entry.SerializeKey,
                entry.Destination,
                (uint)entry.State,
                entry.FormatToken,
                entry.ErrorMessage,
                entry.WorkSubscriptionId,
                entry.ArtworkId,
                payloadJson);
        }).ToList();

        Storage.AddOrReplaceSubscriptionDownloadHistoryBatch(records);
        OnChanged();
    }

    public bool ContainsIdentity(
        int workSubscriptionId,
        string artworkId,
        string destination)
    {
        if (workSubscriptionId <= 0 || string.IsNullOrWhiteSpace(artworkId) || string.IsNullOrWhiteSpace(destination))
            return false;

        return Storage.ContainsSubscriptionDownloadIdentity(workSubscriptionId, artworkId, destination);
    }

    public bool TryDeleteByIdentity(
        int workSubscriptionId,
        string artworkId,
        string destination)
    {
        if (workSubscriptionId <= 0 || string.IsNullOrWhiteSpace(artworkId) || string.IsNullOrWhiteSpace(destination))
            return false;

        var ok = Storage.TryDeleteSubscriptionDownloadByIdentity(workSubscriptionId, artworkId, destination);
        if (ok)
            OnChanged();
        return ok;
    }

    internal int DeleteByWorkSubscriptionId(int workSubscriptionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workSubscriptionId);

        var count = (int)Storage.DeleteSubscriptionDownloadsByWorkSubscriptionId(workSubscriptionId);
        if (count > 0)
            OnChanged();
        return count;
    }

    internal int DeleteOrphans(IReadOnlySet<int> workSubscriptionIds)
    {
        ArgumentNullException.ThrowIfNull(workSubscriptionIds);

        var validIds = workSubscriptionIds.Select(static id => (long)id).ToList();
        var count = (int)Storage.DeleteOrphanSubscriptionDownloads(validIds);
        if (count > 0)
            OnChanged();
        return count;
    }

    public override void Clear()
    {
        Storage.ClearSubscriptionDownloadHistory();
        OnChanged();
    }

    public override async IAsyncEnumerable<SubscriptionDownloadHistoryEntry> StreamEntriesAsync(
        int skip = 0,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var currentSkip = (uint)skip;
        const uint pageSize = 100;
        while (!token.IsCancellationRequested)
        {
            var records = Storage.StreamSubscriptionDownloadHistory(currentSkip, pageSize);
            if (records.Count == 0)
                yield break;

            foreach (var r in records)
            {
                token.ThrowIfCancellationRequested();
                if (r.PayloadJson is not null && TryHydrateSubscriptionEntry(r, out var entry))
                {
                    yield return entry;
                }
            }

            if (records.Count < pageSize)
                yield break;

            currentSkip += (uint)records.Count;
        }
    }

    private static bool TryHydrateSubscriptionEntry(SubscriptionDownloadHistoryRecord r, out SubscriptionDownloadHistoryEntry entry)
    {
        entry = new SubscriptionDownloadHistoryEntry
        {
            HistoryEntryId = (int)r.HistoryEntryId,
            ArtworkId = r.ArtworkId,
            WorkSubscriptionId = (int)r.WorkSubscriptionId,
            SerializeKey = r.SerializeKey ?? "",
            Destination = r.Destination,
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
