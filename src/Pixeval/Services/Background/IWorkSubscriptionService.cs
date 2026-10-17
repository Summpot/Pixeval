// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pixeval.Config;
using Pixeval.Mako;
using Pixeval.Storage;
using Pixeval.Subscription;
using Pixeval.Utilities;

namespace Pixeval.Services.Background;

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

    void QueueInitialSync(WorkSubscriptionRecord subscription, IAsyncEnumerable<IWorkEntry>? source = null);

    void QueueSyncCurrentSource(
        long targetId,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind,
        IAsyncEnumerable<IWorkEntry> source);
}
