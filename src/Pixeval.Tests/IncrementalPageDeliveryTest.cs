// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Common.Collections;

namespace Pixeval.Tests;

[TestClass]
public sealed class IncrementalPageDeliveryTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CompletedPageIsReleasedWhenItsUiContinuationIsCanceled(bool disposalFails)
    {
        var context = new QueuedContext();
        var source = new CompletedSource();
        using var collection = new IncrementalLoadingCollection<TrackedItem>(source, 2);
        var first = new TrackedItem();
        var second = new TrackedItem();
        var disposalError = new InvalidOperationException("Fixed disposal failure");
        if (disposalFails)
            first.DisposeError = disposalError;
        Task<int> loading;
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            loading = collection.LoadMoreItemsAsync(0);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        source.Completion.SetResult([first, second]);
        await context.Posted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(source.Completion.Task.IsCompletedSuccessfully);
        Assert.IsFalse(loading.IsCompleted);
        collection.Dispose();
        context.RunPending();
        if (disposalFails)
        {
            var error = await Assert.ThrowsExactlyAsync<AggregateException>(() => loading);
            Assert.HasCount(2, error.InnerExceptions);
            _ = Assert.IsInstanceOfType<OperationCanceledException>(error.InnerExceptions[0]);
            Assert.AreSame(disposalError, error.InnerExceptions[1]);
        }
        else
            await Assert.ThrowsAsync<OperationCanceledException>(() => loading);

        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
        Assert.IsEmpty(collection);
        Assert.IsFalse(collection.IsLoading);
    }

    private sealed class TrackedItem : IDisposable
    {
        public int DisposeCount { get; private set; }
        public Exception? DisposeError { get; set; }

        public void Dispose()
        {
            DisposeCount++;
            if (DisposeError is { } error)
                throw error;
        }
    }

    private sealed class CompletedSource : IIncrementalSource<TrackedItem>
    {
        public bool HasMoreItems => false;
        public TaskCompletionSource<IReadOnlyCollection<TrackedItem>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyCollection<TrackedItem>> GetPagedItemsAsync(int pageIndex, int pageSize, CancellationToken token = default)
            => Completion.Task;
    }

    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        public TaskCompletionSource Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Post(SendOrPostCallback d, object? state)
        {
            _callbacks.Enqueue((d, state));
            _ = Posted.TrySetResult();
        }

        public void RunPending()
        {
            var previous = Current;
            try
            {
                SetSynchronizationContext(this);
                while (_callbacks.TryDequeue(out var item))
                    item.Callback(item.State);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }
}
