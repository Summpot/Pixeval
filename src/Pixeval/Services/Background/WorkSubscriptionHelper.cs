// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Pixeval.Config;
using Pixeval.Mako;
using Pixeval.Pixiv;
using Pixeval.Storage;
using Pixeval.Utilities;

namespace Pixeval.Services.Background;

public static class WorkSubscriptionHelper
{
    public static bool TryAddOrUpdateUser(
        User user,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind,
        StorageEngine storageEngine,
        IWorkSubscriptionService subscriptions)
    {
        var subscription = new WorkSubscriptionRecord(
            user.Id,
            subscriptionType,
            workKind,
            user.Name,
            user.Account,
            user.AvatarUrl);
        return TryAddOrUpdate(subscription, storageEngine, subscriptions);
    }

    public static bool TryAddOrUpdateSeries(
        long seriesId,
        WorkSubscriptionWorkKind workKind,
        StorageEngine storageEngine,
        IWorkSubscriptionService subscriptions,
        Series? seriesDetail = null,
        IWorkEntry? firstWork = null,
        IAsyncEnumerable<IWorkEntry>? source = null)
    {
        var title = seriesDetail?.Title ?? firstWork?.Title ?? "";
        var avatar = seriesDetail?.CoverUrl ?? firstWork?.GetThumbnailUrl() ?? "";
        var author = seriesDetail?.User?.Name ?? firstWork?.User.Name ?? "";
        var subscription = new WorkSubscriptionRecord(
            seriesId,
            WorkSubscriptionType.Series,
            workKind,
            title,
            author,
            avatar);
        return TryAddOrUpdate(subscription, storageEngine, subscriptions, source);
    }

    private static bool TryAddOrUpdate(
        WorkSubscriptionRecord subscription,
        StorageEngine storageEngine,
        IWorkSubscriptionService subscriptions,
        IAsyncEnumerable<IWorkEntry>? source = null)
    {
        if (subscription.Id is 0)
            return false;

        var saved = storageEngine.UpsertSubscription(subscription);
        subscriptions.QueueInitialSync(saved, source);
        return true;
    }
}
