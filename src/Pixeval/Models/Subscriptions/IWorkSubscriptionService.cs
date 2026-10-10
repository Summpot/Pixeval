// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Pixeval.Models.Options;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Native.Subscription;
using Pixeval.Utilities;

namespace Pixeval.Models.Subscriptions;

public interface IWorkSubscriptionService
{
    SubscriptionFetchState? CurrentFetchState { get; }

    event EventHandler<SubscriptionFetchState>? FetchStateChanged;

    event EventHandler<WorkSubscriptionRecord>? SubscriptionUpdated;

    event EventHandler<long>? SubscriptionRemoved;

    event EventHandler<uint>? NewWorksIngested;

    event EventHandler<bool>? DaemonStateChanged;

    bool IsDaemonRunning { get; }

    void StartDaemon(ulong intervalSecs = 1800);

    void StopDaemon();

    void SetDaemonInterval(ulong intervalSecs);

    WorkSubscriptionRecord? TryGetSubscription(
        long targetId,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind);

    Task<WorkSubscriptionRecord?> TryRemoveAsync(long historyEntryId);

    void QueueSyncAll();

    void QueueSyncSubscription(WorkSubscriptionRecord subscription);

    void QueueInitialSync(WorkSubscriptionRecord subscription, IFetchEngine<IWorkEntry>? sourceEngine = null);

    void QueueSyncCurrentSource(
        long targetId,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind,
        IFetchEngine<IWorkEntry> engine);
}
