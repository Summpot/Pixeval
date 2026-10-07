// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixeval.Native.Storage;

public partial class DownloadRepository
{
    public void Update(IDownloadHistoryEntry entry)
    {
        switch (entry)
        {
            case DownloadHistoryRecord downloadRecord:
                UpdateDownloadHistoryState(
                    downloadRecord.Destination,
                    (uint)downloadRecord.State,
                    downloadRecord.ErrorMessage);
                break;
            case SubscriptionDownloadHistoryRecord subscriptionRecord:
                UpdateSubscriptionDownloadHistoryState(
                    subscriptionRecord.WorkSubscriptionId,
                    subscriptionRecord.ArtworkId,
                    subscriptionRecord.Destination,
                    (uint)subscriptionRecord.State,
                    subscriptionRecord.ErrorMessage);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entry));
        }
    }

    public void AddOrReplace(IDownloadHistoryEntry entry)
    {
        switch (entry)
        {
            case DownloadHistoryRecord downloadRecord:
                AddOrReplaceDownloadHistory(
                    downloadRecord.Id,
                    downloadRecord.SerializeKey,
                    downloadRecord.Destination,
                    (uint)downloadRecord.State,
                    downloadRecord.FormatToken,
                    downloadRecord.ErrorMessage,
                    downloadRecord.PayloadJson ?? "");
                break;
            case SubscriptionDownloadHistoryRecord subscriptionRecord:
                AddOrReplaceSubscriptionDownloadHistory(
                    subscriptionRecord.Id,
                    subscriptionRecord.SerializeKey,
                    subscriptionRecord.Destination,
                    (uint)subscriptionRecord.State,
                    subscriptionRecord.FormatToken,
                    subscriptionRecord.ErrorMessage,
                    subscriptionRecord.WorkSubscriptionId,
                    subscriptionRecord.ArtworkId,
                    subscriptionRecord.PayloadJson ?? "");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entry));
        }
    }

    public bool TryDelete(IDownloadHistoryEntry entry)
    {
        return entry switch
        {
            DownloadHistoryRecord downloadRecord =>
                TryDeleteDownloadHistoryByDestination(downloadRecord.Destination),
            SubscriptionDownloadHistoryRecord subscriptionRecord =>
                TryDeleteSubscriptionDownloadByIdentity(
                    subscriptionRecord.WorkSubscriptionId,
                    subscriptionRecord.ArtworkId,
                    subscriptionRecord.Destination),
            _ => throw new ArgumentOutOfRangeException(nameof(entry))
        };
    }
}
