// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#pragma warning disable CS0436

using System.Collections.Generic;
using UniFFISharp;

namespace Pixeval.Subscription;

public static partial class PixevalSubscriptionMethods
{
    [PreferUniFFI]
    public static partial SubscriptionFolderSnapshot ComputeSubscriptionFolderSnapshot(
        long subscriptionId,
        List<FolderTaskItemState> items,
        bool isFetching,
        uint fetchedCount,
        long? retryAtTimestamp);
}
