// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Subscription;

namespace Pixeval.Tests;

[TestClass]
public sealed class SubscriptionEngineTest
{
    private sealed class TestProgressCallback : ISubscriptionProgressCallback
    {
        public SubscriptionSync? Engine { get; set; }

        public List<SubscriptionFetchState> StateChanges { get; } = [];
        public List<(long SubId, uint DuplicateCount)> DuplicateStoppedEvents { get; } = [];
        public List<SubscriptionDownloadItem> FetchedItems { get; } = [];
        public List<uint> NewWorksIngestedCounts { get; } = [];
        public List<bool> DaemonStateChanges { get; } = [];

        public void OnFetchStateChanged(SubscriptionFetchState state)
        {
            _ = Engine?.GetFetchState(state.SubscriptionId);
            StateChanges.Add(state);
        }

        public void OnDuplicateStopped(long subscriptionId, uint duplicateCount)
        {
            _ = Engine?.GetPendingCount();
            DuplicateStoppedEvents.Add((subscriptionId, duplicateCount));
        }

        public void OnItemFetched(SubscriptionDownloadItem item)
        {
            FetchedItems.Add(item);
        }

        public void OnSubscriptionUpdated(Pixeval.Storage.WorkSubscriptionRecord record)
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
            _ = Engine?.IsDaemonRunning();
            DaemonStateChanges.Add(isRunning);
        }
    }

    [TestMethod]
    public async Task ProgressCallbacksCanReenterNativeStateWithoutHoldingItsLock()
    {
        var callback = new TestProgressCallback();
        using var engine = new SubscriptionSync(1, callback);
        callback.Engine = engine;
        await Task.Run(() =>
        {
            engine.SetFetchState(42, 1, 1, SubscriptionStatus.Fetching);
            engine.RecordWorkProcessed(42, true);
            engine.StartDaemon(600);
            engine.StopDaemon();
        }).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.HasCount(1, callback.StateChanges);
        Assert.HasCount(1, callback.DuplicateStoppedEvents);
        callback.Engine = null;
    }

    [TestMethod]
    public void DuplicateFuseBreakerShouldTriggerAtThresholdFive()
    {
        using var engine = new SubscriptionSync(5, null);

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
        using var engine = new SubscriptionSync(5, null);

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
        using var engine = new SubscriptionSync(5, null);

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
        using var engine = new SubscriptionSync(5, callback);

        var initialState = engine.GetFetchState(400);
        Assert.IsNull(initialState);

        engine.SetFetchState(400, 1, 10, SubscriptionStatus.Fetching);
        var runningState = engine.GetFetchState(400);
        Assert.IsNotNull(runningState);
        Assert.AreEqual(SubscriptionStatus.Fetching, runningState.Value.Status);
        Assert.AreEqual(1u, runningState.Value.Page);
        Assert.AreEqual(10u, runningState.Value.TotalFetched);

        engine.SetFetchState(400, 2, 20, SubscriptionStatus.Completed);
        var completedState = engine.GetFetchState(400);
        Assert.IsNotNull(completedState);
        Assert.AreEqual(SubscriptionStatus.Completed, completedState.Value.Status);

        Assert.AreEqual(2, callback.StateChanges.Count);
        Assert.AreEqual(SubscriptionStatus.Fetching, callback.StateChanges[0].Status);
        Assert.AreEqual(SubscriptionStatus.Completed, callback.StateChanges[1].Status);
    }

    [TestMethod]
    public void QueueArbitrationSyncAllShouldSupersedeIndividual()
    {
        using var engine = new SubscriptionSync(null, null);

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
        using var engine = new SubscriptionSync(5, callback);

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
