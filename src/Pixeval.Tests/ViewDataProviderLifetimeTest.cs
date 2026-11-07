// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Mako.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Collections;
using Pixeval.ViewModels;

namespace Pixeval.Tests;

[TestClass]
public sealed class ViewDataProviderLifetimeTest
{
    [TestMethod]
    public async Task ResetDisposesUnpublishedItemsAndRejectsLateResults()
    {
        using var provider = new SimpleViewDataProvider<Illustration, TrackedEntry>();
        var oldEngine = new ControlledEntries();
        var created = new List<TrackedEntry>();
        provider.ResetEngine(oldEngine, (entry, _) => Track(created, entry), 2);
        var oldSource = (IncrementalLoadingCollection<TrackedEntry>) provider.Source;
        var loading = oldSource.LoadMoreItemsAsync(0);
        await oldEngine.Pending.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.HasCount(1, created);
        Assert.IsEmpty(oldSource);

        var newEngine = new ControlledEntries(10);
        provider.ResetEngine(newEngine, (entry, _) => Track(created, entry), 1);
        Assert.IsTrue(oldEngine.Token.IsCancellationRequested);
        Assert.AreEqual(0, oldEngine.DisposeCount);
        oldEngine.Completion.SetResult(Illustration.CreateDefault() with { Id = 2 });
        await Assert.ThrowsAsync<OperationCanceledException>(() => loading);

        Assert.HasCount(1, created);
        Assert.AreEqual(1, created[0].DisposeCount);
        Assert.AreEqual(1, oldEngine.DisposeCount);
        Assert.IsEmpty(oldSource);
        Assert.IsEmpty(provider.Source);
        Assert.AreEqual(1, await ((IIncrementalLoading) provider.Source).LoadMoreItemsAsync(0));
        Assert.AreEqual(10L, provider.Source[0].Entry.Id);
        provider.Dispose();
        provider.Dispose();
        Assert.AreEqual(1, created[1].DisposeCount);
        Assert.AreEqual(1, newEngine.DisposeCount);
    }

    [TestMethod]
    public async Task FailedPageDisposesUnpublishedItemsAndReportsOriginalError()
    {
        using var provider = new SimpleViewDataProvider<Illustration, TrackedEntry>();
        var engine = new ControlledEntries();
        var created = new List<TrackedEntry>();
        provider.ResetEngine(engine, (entry, _) => Track(created, entry), 2);
        var source = (IncrementalLoadingCollection<TrackedEntry>) provider.Source;
        Exception? reported = null;
        source.OnError += exception => reported = exception;
        var loading = source.LoadMoreItemsAsync(0);
        await engine.Pending.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var failure = new InvalidOperationException("Fixed page failure");
        engine.Completion.SetException(failure);

        Assert.AreEqual(0, await loading);
        Assert.AreSame(failure, reported);
        Assert.HasCount(1, created);
        Assert.AreEqual(1, created[0].DisposeCount);
        Assert.IsEmpty(source);
        Assert.IsFalse(source.HasMoreItems);
        Assert.IsFalse(source.IsLoading);
        provider.Dispose();
        Assert.AreEqual(1, created[0].DisposeCount);
        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task ClosingTheProviderDuringTheFirstAddReleasesTheRestOfThePage()
    {
        using var provider = new SimpleViewDataProvider<Illustration, TrackedEntry>();
        var engine = new ControlledEntries();
        engine.Completion.SetResult(Illustration.CreateDefault() with { Id = 2 });
        var created = new List<TrackedEntry>();
        provider.ResetEngine(engine, (entry, _) => Track(created, entry), 2);
        var source = (IncrementalLoadingCollection<TrackedEntry>) provider.Source;
        source.CollectionChanged += (_, change) =>
        {
            if (change.Action is NotifyCollectionChangedAction.Add)
                provider.Dispose();
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => source.LoadMoreItemsAsync(0));

        Assert.HasCount(2, created);
        Assert.AreEqual(1, created[0].DisposeCount);
        Assert.AreEqual(1, created[1].DisposeCount);
        Assert.IsEmpty(source);
        Assert.IsFalse(source.IsLoading);
        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task SourceDisposalRejectsLateSuccessAndEndOfSequence(bool lateItem)
    {
        var engine = new ControlledEntries();
        var created = new List<TrackedEntry>();
        using var source = new IncrementalSource<Illustration, TrackedEntry>(engine, (entry, _) => Track(created, entry));
        var loading = source.GetPagedItemsAsync(0, 2);
        await engine.Pending.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.Dispose();
        Assert.IsTrue(engine.Token.IsCancellationRequested);
        Assert.AreEqual(0, engine.DisposeCount);
        engine.Completion.SetResult(lateItem ? Illustration.CreateDefault() with { Id = 2 } : null);

        await Assert.ThrowsAsync<OperationCanceledException>(() => loading);
        Assert.HasCount(1, created);
        Assert.AreEqual(1, created[0].DisposeCount);
        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task SharedSourceRemainsLiveUntilItsLastViewCloses()
    {
        using var first = new SharableViewDataProvider<Illustration, TrackedEntry>();
        var engine = new ControlledEntries();
        var created = new List<TrackedEntry>();
        first.ResetEngine(engine, (entry, _) => Track(created, entry), 2);
        using var second = first.CloneRef();
        var shared = (IncrementalLoadingCollection<TrackedEntry>) first.Source;
        Assert.AreSame(shared, second.Source);
        var loading = shared.LoadMoreItemsAsync(0);
        await engine.Pending.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var replacement = new ControlledEntries(10);
        first.ResetEngine(replacement, (entry, _) => Track(created, entry), 1);
        Assert.IsFalse(engine.Token.IsCancellationRequested);
        Assert.AreEqual(0, created[0].DisposeCount);
        engine.Completion.SetResult(Illustration.CreateDefault() with { Id = 2 });
        Assert.AreEqual(2, await loading);
        Assert.HasCount(2, second.Source);
        Assert.IsEmpty(first.Source);
        Assert.AreSame(created[0], second.View[0]);
        Assert.AreSame(created[1], second.View[1]);

        second.Dispose();
        second.Dispose();
        Assert.AreEqual(1, engine.DisposeCount);
        Assert.AreEqual(1, created[0].DisposeCount);
        Assert.AreEqual(1, created[1].DisposeCount);
        Assert.AreEqual(0, replacement.DisposeCount);
        Assert.AreEqual(1, await ((IIncrementalLoading) first.Source).LoadMoreItemsAsync(0));
        first.Dispose();
        Assert.AreEqual(1, created[2].DisposeCount);
        Assert.AreEqual(1, replacement.DisposeCount);
    }

    [TestMethod]
    public async Task CleanupContinuesAfterDisposalFailuresAndPreservesThePageError()
    {
        var failure = new InvalidOperationException("Fixed page failure");
        var firstFailure = new InvalidOperationException("First disposal failure");
        var secondFailure = new InvalidOperationException("Second disposal failure");
        var created = new List<TrackedEntry>();
        using var source = new IncrementalSource<Illustration, TrackedEntry>(FailingEntries(failure), (entry, index) =>
        {
            var result = Track(created, entry);
            result.DisposeError = index is 0 ? firstFailure : secondFailure;
            return result;
        });

        var error = await Assert.ThrowsExactlyAsync<AggregateException>(() => source.GetPagedItemsAsync(0, 3));

        Assert.AreSequenceEqual(new Exception[] { failure, firstFailure, secondFailure }, error.InnerExceptions);
        Assert.HasCount(2, created);
        Assert.AreEqual(1, created[0].DisposeCount);
        Assert.AreEqual(1, created[1].DisposeCount);
        Assert.IsFalse(source.HasMoreItems);
    }

    [TestMethod]
    public async Task FactoryFailureReleasesEarlierItemsWithoutReplacingTheError()
    {
        var failure = new InvalidOperationException("Fixed factory failure");
        var created = new List<TrackedEntry>();
        using var source = new IncrementalSource<Illustration, TrackedEntry>(FailingEntries(failure), (entry, index) =>
            index is 0 ? Track(created, entry) : throw failure);

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => source.GetPagedItemsAsync(0, 3));

        Assert.AreSame(failure, error);
        Assert.HasCount(1, created);
        Assert.AreEqual(1, created[0].DisposeCount);
        Assert.IsFalse(source.HasMoreItems);
    }

    private static async IAsyncEnumerable<Illustration> FailingEntries(Exception failure)
    {
        yield return Illustration.CreateDefault() with { Id = 1 };
        yield return Illustration.CreateDefault() with { Id = 2 };
        await Task.CompletedTask;
        throw failure;
    }

    private static TrackedEntry Track(List<TrackedEntry> created, Illustration entry)
    {
        var result = new TrackedEntry(entry);
        created.Add(result);
        return result;
    }

    private sealed class TrackedEntry(Illustration entry) : EntryViewModel<Illustration>(entry), IDisposable
    {
        public override Uri AppUri => new("pixeval://illustration/1");
        public override Uri WebsiteUri => new("https://www.pixiv.net/artworks/1");
        public int DisposeCount { get; private set; }
        public Exception? DisposeError { get; set; }

        public void Dispose()
        {
            DisposeCount++;
            if (DisposeError is { } error)
                throw error;
        }
    }

    // Deliberately ignores cancellation while awaiting the second response, like a late transport callback.
    private sealed class ControlledEntries(long firstId = 1) : IAsyncEnumerable<Illustration>, IAsyncEnumerator<Illustration>
    {
        private int _moveCount;
        public CancellationToken Token { get; private set; }
        public int DisposeCount { get; private set; }
        public TaskCompletionSource Pending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Illustration?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Illustration Current { get; private set; } = null!;

        public IAsyncEnumerator<Illustration> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            return this;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (++_moveCount is 1)
            {
                Current = Illustration.CreateDefault() with { Id = firstId };
                return true;
            }
            if (_moveCount is not 2)
                return false;
            Pending.SetResult();
            var entry = await Completion.Task;
            Current = entry!;
            return entry is not null;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
