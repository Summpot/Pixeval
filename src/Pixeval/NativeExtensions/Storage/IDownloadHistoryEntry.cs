// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Native.Download;
using Pixeval.Native.Mako;
using MakoNovel = Pixeval.Native.Mako.Novel;

namespace Pixeval.Native.Storage;

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
        var id = entry switch
        {
            Illustration i => i.Id.ToString(),
            MakoNovel n => n.Id.ToString(),
            Booru.BooruPost b => b.Id,
            SauceNao.SauceNaoItem s => s.RawId,
            _ => ""
        };

        var serializable = entry as IArtworkSerializable;
        var serializeKey = serializable?.SerializeKey;
        var payloadJson = serializable?.Serialize();

        if (workSubscriptionId is > 0 and var subId)
        {
            var record = new SubscriptionDownloadHistoryRecord(
                0,
                id,
                serializeKey,
                destination,
                (uint)DownloadState.Queued,
                null,
                null,
                subId,
                id,
                payloadJson)
            {
                EntryOverride = entry
            };
            return record;
        }

        var dlRecord = new DownloadHistoryRecord(
            0,
            id,
            serializeKey,
            destination,
            (uint)DownloadState.Queued,
            null,
            null,
            payloadJson)
        {
            EntryOverride = entry
        };
        return dlRecord;
    }
}
