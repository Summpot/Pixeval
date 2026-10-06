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

public sealed class WatchLaterPersistentManager : WorkHistoryPersistentManager<WatchLaterEntry>
{
    private readonly HashSet<string> _workKeys;

    public WatchLaterPersistentManager(StorageEngine storage, FileLogger logger) : base(storage, logger)
    {
        _workKeys = storage.StreamWatchLater(0, 1000000)
            .Select(static entry => entry.WorkKey)
            .Where(static workKey => !string.IsNullOrWhiteSpace(workKey))
            .ToHashSet(StringComparer.Ordinal);
    }

    public override int Count => (int)Storage.CountWatchLater();

    public bool ContainsWorkKey(string workKey)
    {
        if (string.IsNullOrWhiteSpace(workKey))
            return false;

        lock (_workKeys)
        {
            if (_workKeys.Contains(workKey))
                return true;
        }

        return Storage.ContainsWatchLater(workKey);
    }

    public override WatchLaterEntry? GetByWorkKey(string workKey)
    {
        if (string.IsNullOrWhiteSpace(workKey))
            return null;

        var r = Storage.GetWatchLaterByWorkKey(workKey);
        if (r is null || r.PayloadJson is null)
            return null;

        return TryHydrateWatchLaterEntry(r, out var entry) ? entry : null;
    }

    public override void AddOrReplace(WatchLaterEntry entry)
    {
        var payloadJson = entry.Payload?.SerializedArtwork ?? (entry.Entry as Misaki.ISerializable)?.Serialize() ?? "";
        var r = Storage.AddOrReplaceWatchLater(entry.Id, entry.SerializeKey, entry.WorkKey, payloadJson);
        entry.HistoryEntryId = (int)r.HistoryEntryId;
        lock (_workKeys)
            _workKeys.Add(entry.WorkKey);
        OnChanged();
    }

    public override bool TryDeleteByWorkKey(string workKey)
    {
        if (string.IsNullOrWhiteSpace(workKey))
            return false;

        var removed = Storage.RemoveWatchLater(workKey);
        if (removed)
        {
            lock (_workKeys)
                _workKeys.Remove(workKey);
            OnChanged();
        }
        return removed;
    }

    public override void Clear()
    {
        Storage.ClearWatchLater();
        lock (_workKeys)
            _workKeys.Clear();
        OnChanged();
    }

    public override async IAsyncEnumerable<WatchLaterEntry> StreamEntriesAsync(
        int skip = 0,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var currentSkip = (uint)skip;
        const uint pageSize = 100;
        while (!token.IsCancellationRequested)
        {
            var records = Storage.StreamWatchLater(currentSkip, pageSize);
            if (records.Count == 0)
                yield break;

            foreach (var r in records)
            {
                token.ThrowIfCancellationRequested();
                if (r.PayloadJson is not null && TryHydrateWatchLaterEntry(r, out var entry))
                {
                    yield return entry;
                }
            }

            if (records.Count < pageSize)
                yield break;

            currentSkip += (uint)records.Count;
        }
    }

    private static bool TryHydrateWatchLaterEntry(WatchLaterRecord r, out WatchLaterEntry entry)
    {
        entry = new WatchLaterEntry
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

    protected override void OnEntryInserted(WatchLaterEntry entry)
    {
        lock (_workKeys)
            _workKeys.Add(entry.WorkKey);
    }

    protected override void OnEntriesDeleted(IReadOnlyCollection<WatchLaterEntry> entries)
    {
        lock (_workKeys)
        {
            foreach (var entry in entries)
                _ = _workKeys.Remove(entry.WorkKey);
        }
    }

    protected override void OnEntriesCleared()
    {
        lock (_workKeys)
            _workKeys.Clear();
    }
}
