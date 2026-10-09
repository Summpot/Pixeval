// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native.Subscription;

namespace Pixeval.Tests;

[TestClass]
public sealed class SubscriptionEngineTest
{
    private sealed class TestProgressCallback : ISubscriptionProgressCallback
    {
        public List<SubscriptionFetchState> StateChanges { get; } = [];
        public List<(long SubId, uint DuplicateCount)> DuplicateStoppedEvents { get; } = [];
        public List<SubscriptionDownloadItem> FetchedItems { get; } = [];
        public List<uint> NewWorksIngestedCounts { get; } = [];
        public List<bool> DaemonStateChanges { get; } = [];

        public void OnFetchStateChanged(SubscriptionFetchState state)
        {
            StateChanges.Add(state);
        }

        public void OnDuplicateStopped(long subscriptionId, uint duplicateCount)
        {
            DuplicateStoppedEvents.Add((subscriptionId, duplicateCount));
        }

        public void OnItemFetched(SubscriptionDownloadItem item)
        {
            FetchedItems.Add(item);
        }

        public void OnSubscriptionUpdated(long subscriptionId, string name, string account, string avatarUrl)
        {
        }

        public void OnSyncFinished()
        {
        }

        public void OnNewWorksIngested(uint totalCount)
        {
            NewWorksIngestedCounts.Add(totalCount);
        }

        public void OnDaemonStateChanged(bool isRunning)
        {
            DaemonStateChanges.Add(isRunning);
        }
    }

    [TestMethod]
    public void DuplicateFuseBreakerShouldTriggerAtThresholdFive()
    {
        using var engine = new SubscriptionSyncEngine(5, null);

        // 1 to 4 duplicates should return false
        for (var i = 0; i < 4; i++)
        {
            var reached = engine.RecordWorkProcessed(100, true);
            Assert.IsFalse(reached, $"Duplicate count {i + 1} should not reach threshold");
        }

        // 5th duplicate reaches threshold
        var fifthReached = engine.RecordWorkProcessed(100, true);
        Assert.IsTrue(fifthReached, "5th duplicate should trigger fuse breaker");
    }

    [TestMethod]
    public void NonDuplicateWorkShouldResetDuplicateCounter()
    {
        using var engine = new SubscriptionSyncEngine(5, null);

        // 4 duplicates
        for (var i = 0; i < 4; i++)
        {
            Assert.IsFalse(engine.RecordWorkProcessed(200, true));
        }

        // 1 non-duplicate resets
        Assert.IsFalse(engine.RecordWorkProcessed(200, false));

        // Next 4 duplicates should not trigger
        for (var i = 0; i < 4; i++)
        {
            Assert.IsFalse(engine.RecordWorkProcessed(200, true));
        }

        // 5th duplicate triggers
        Assert.IsTrue(engine.RecordWorkProcessed(200, true));
    }

    [TestMethod]
    public void ResetDuplicateCounterExplicitlyShouldSucceed()
    {
        using var engine = new SubscriptionSyncEngine(5, null);

        for (var i = 0; i < 4; i++)
        {
            Assert.IsFalse(engine.RecordWorkProcessed(300, true));
        }

        engine.ResetDuplicateCounter(300);

        for (var i = 0; i < 4; i++)
        {
            Assert.IsFalse(engine.RecordWorkProcessed(300, true));
        }

        Assert.IsTrue(engine.RecordWorkProcessed(300, true));
    }

    [TestMethod]
    public void FetchStateTransitionAndCallbackShouldWork()
    {
        var callback = new TestProgressCallback();
        using var engine = new SubscriptionSyncEngine(5, callback);

        var initialState = engine.GetFetchState(400);
        Assert.IsNull(initialState);

        engine.SetFetchState(400, 1, 10, SubscriptionStatus.Fetching);
        var runningState = engine.GetFetchState(400);
        Assert.IsNotNull(runningState);
        Assert.AreEqual(SubscriptionStatus.Fetching, runningState.Status);
        Assert.AreEqual(1u, runningState.Page);
        Assert.AreEqual(10u, runningState.TotalFetched);

        engine.SetFetchState(400, 2, 20, SubscriptionStatus.Completed);
        var completedState = engine.GetFetchState(400);
        Assert.IsNotNull(completedState);
        Assert.AreEqual(SubscriptionStatus.Completed, completedState.Status);

        Assert.AreEqual(2, callback.StateChanges.Count);
        Assert.AreEqual(SubscriptionStatus.Fetching, callback.StateChanges[0].Status);
        Assert.AreEqual(SubscriptionStatus.Completed, callback.StateChanges[1].Status);
    }

    [TestMethod]
    public void QueueArbitrationSyncAllShouldSupersedeIndividual()
    {
        using var engine = new SubscriptionSyncEngine(null, null);

        Assert.IsTrue(engine.QueueSyncSubscription(1));
        Assert.IsTrue(engine.QueueSyncSubscription(2));

        // QueueSyncAll supersedes single items
        Assert.IsTrue(engine.QueueSyncAll());

        // Further QueueSyncAll is deduped
        Assert.IsFalse(engine.QueueSyncAll());

        // Dequeue should return SyncAll
        var next = engine.TryDequeueSyncRequest();
        Assert.IsNotNull(next);
        Assert.IsTrue(next is SyncRequestKind.All);

        // Queue should now be empty
        Assert.IsNull(engine.TryDequeueSyncRequest());
    }

    [TestMethod]
    public void DaemonLifecycleAndIntervalControlShouldWork()
    {
        var callback = new TestProgressCallback();
        using var engine = new SubscriptionSyncEngine(5, callback);

        Assert.IsFalse(engine.IsDaemonRunning());
        Assert.AreEqual(1800UL, engine.GetDaemonInterval());

        engine.SetDaemonInterval(3600UL);
        Assert.AreEqual(3600UL, engine.GetDaemonInterval());

        engine.StartDaemon(600UL);
        Assert.IsTrue(engine.IsDaemonRunning());
        Assert.AreEqual(600UL, engine.GetDaemonInterval());

        engine.StopDaemon();
        Assert.IsFalse(engine.IsDaemonRunning());
    }
}
