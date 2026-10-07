// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Models.Options;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.Models.Subscriptions;

public static class WorkSubscriptionHelper
{
    public static bool TryAddOrUpdateUser(
        User user,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind)
    {
        var subscription = new WorkSubscriptionRecord(
            user.Id,
            subscriptionType,
            workKind,
            user.Name,
            user.Account,
            user.AvatarUrl);
        return TryAddOrUpdate(subscription);
    }

    public static bool TryAddOrUpdateSeries(
        long seriesId,
        WorkSubscriptionWorkKind workKind,
        Series? seriesDetail = null,
        IWorkEntry? firstWork = null,
        IFetchEngine<IWorkEntry>? sourceEngine = null)
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
        return TryAddOrUpdate(subscription, sourceEngine);
    }

    private static bool TryAddOrUpdate(
        WorkSubscriptionRecord subscription,
        IFetchEngine<IWorkEntry>? sourceEngine = null)
    {
        if (subscription.Id is 0)
            return false;

        var storageEngine = App.AppViewModel.StorageEngine;
        var saved = storageEngine.UpsertSubscription(subscription);

        App.AppViewModel.QueueWorkSubscriptionInitialSync(saved, sourceEngine);
        return true;
    }
}
