// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Download;

namespace Pixeval.Storage;

public interface IDownloadHistoryEntry
{
    long HistoryEntryId { get; }
    long Id => HistoryEntryId;

    string Destination { get; }

    DownloadState State { get; set; }

    string? FormatToken { get; set; }

    string? ErrorMessage { get; set; }

    object? Entry { get; }

    DownloadTaskKey DownloadTaskKey { get; }

    public static IDownloadHistoryEntry Create(
        string destination,
        object entry,
        int? workSubscriptionId = null)
    {
        if (!ArtworkPayloads.TryWrap(entry, out var payload))
            throw new ArgumentException("Entry is not a persistable artwork payload.", nameof(entry));

        var id = PixevalStorageMethods.ArtworkPayloadId(payload);

        if (workSubscriptionId is > 0 and var subId)
        {
            var record = new SubscriptionDownloadHistoryRecord(
                0,
                id,
                PixevalStorageMethods.ArtworkSerializeKey(payload),
                destination,
                (uint)DownloadState.Queued,
                null,
                null,
                subId,
                id,
                null,
                payload)
            {
                EntryOverride = entry
            };
            return record;
        }

        var dlRecord = new DownloadHistoryRecord(
            0,
            id,
            PixevalStorageMethods.ArtworkSerializeKey(payload),
            destination,
            (uint)DownloadState.Queued,
            null,
            null,
            null,
            payload)
        {
            EntryOverride = entry
        };
        return dlRecord;
    }
}
