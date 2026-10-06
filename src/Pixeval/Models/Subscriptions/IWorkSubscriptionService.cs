// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Pixeval.Models.Options;
using Pixeval.Native.Storage;
using Pixeval.Native.Subscription;

namespace Pixeval.Models.Subscriptions;

public interface IWorkSubscriptionService
{
    SubscriptionFetchState? CurrentFetchState { get; }

    event EventHandler<SubscriptionFetchState>? FetchStateChanged;

    event EventHandler<WorkSubscriptionRecord>? SubscriptionUpdated;

    event EventHandler<long>? SubscriptionRemoved;

    WorkSubscriptionRecord? TryGetSubscription(
        long targetId,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind);

    Task<WorkSubscriptionRecord?> TryRemoveAsync(long historyEntryId);
}
