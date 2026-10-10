using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Collections;

namespace Pixeval.Tests;

[TestClass]
public sealed class FilteredIncrementalCollectionTest
{
    private sealed class ControlledIncrementalSource<T> : IIncrementalSource<T>, IDisposable
    {
        private readonly Lock _gate = new();
        private readonly List<Call> _calls = [];
        private TaskCompletionSource _callChanged = CreateCompletionSource();

        public int CallCount
        {
            get
            {
                lock (_gate)
                {
                    return _calls.Count;
                }
            }
        }

        public bool IsDisposed { get; private set; }

        public bool HasMoreItems { get; set; } = true;

        public async Task<IReadOnlyCollection<T>> GetPagedItemsAsync(int pageIndex, int pageSize, CancellationToken token = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            var call = new Call(pageIndex, pageSize);
            lock (_gate)
            {
                _calls.Add(call);
                _callChanged.SetResult();
                _callChanged = CreateCompletionSource();
            }

            return await call.Completion.Task.WaitAsync(token);
        }

        public void Dispose() => IsDisposed = true;

        public async Task<Call> WaitForCallAsync(int index)
        {
            while (true)
            {
                Task waitTask;
                lock (_gate)
                {
                    if (_calls.Count > index)
                        return _calls[index];

                    waitTask = _callChanged.Task;
                }

                await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        private static TaskCompletionSource CreateCompletionSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        public sealed class Call(int pageIndex, int pageSize)
        {
            public int PageIndex { get; } = pageIndex;

            public int PageSize { get; } = pageSize;

            public TaskCompletionSource<IReadOnlyCollection<T>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private static async Task AssertOperationCanceledAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Assert.Fail("Expected the task to be canceled.");
    }

    [TestMethod]
    public async Task Test_IncrementalLoadingCollection_CoalescesConcurrentLoads()
    {
        var source = new ControlledIncrementalSource<int>();
        var collection = new IncrementalLoadingCollection<int>(source, 3);

        var first = collection.LoadMoreItemsAsync(0);
        var call = await source.WaitForCallAsync(0);
        var second = collection.LoadMoreItemsAsync(0);
        var third = collection.LoadMoreItemsAsync(0);

        Assert.AreEqual(1, source.CallCount);

        call.Completion.SetResult([1, 2, 3]);
        Assert.AreSequenceEqual((int[]) [3, 3, 3], await Task.WhenAll(first, second, third));
        Assert.AreSequenceEqual((int[]) [1, 2, 3], collection.ToArray());
        Assert.IsTrue(collection.HasMoreItems);
    }

    [TestMethod]
    public async Task Test_IncrementalLoadingCollection_CancelingDuplicateWaitDoesNotCancelSharedLoad()
    {
        var source = new ControlledIncrementalSource<int>();
        var collection = new IncrementalLoadingCollection<int>(source, 3);

        var first = collection.LoadMoreItemsAsync(0);
        var call = await source.WaitForCallAsync(0);
        using var cts = new CancellationTokenSource();
        var second = collection.LoadMoreItemsAsync(0, cts.Token);

        await cts.CancelAsync();
        await AssertOperationCanceledAsync(second);

        Assert.AreEqual(1, source.CallCount);

        call.Completion.SetResult([1, 2, 3]);
        Assert.AreEqual(3, await first);
        Assert.AreSequenceEqual((int[]) [1, 2, 3], collection.ToArray());
        Assert.IsTrue(collection.HasMoreItems);
    }

    [TestMethod]
    public async Task Test_IncrementalLoadingCollection_CancelingCallerDoesNotCancelSharedLoad()
    {
        var source = new ControlledIncrementalSource<int>();
        var collection = new IncrementalLoadingCollection<int>(source, 3);
        using var cts = new CancellationTokenSource();

        var canceledLoad = collection.LoadMoreItemsAsync(0, cts.Token);
        var canceledCall = await source.WaitForCallAsync(0);
        await cts.CancelAsync();

        await AssertOperationCanceledAsync(canceledLoad);
        Assert.IsEmpty(collection);
        Assert.IsTrue(collection.HasMoreItems);
        Assert.AreEqual(0, canceledCall.PageIndex);

        var sharedLoad = collection.LoadMoreItemsAsync(0);
        Assert.AreEqual(1, source.CallCount);
        canceledCall.Completion.SetResult([1, 2, 3]);

        Assert.AreEqual(3, await sharedLoad);
        Assert.AreEqual(0, canceledCall.PageIndex);
        Assert.AreSequenceEqual((int[]) [1, 2, 3], collection.ToArray());
    }

    [TestMethod]
    public async Task Test_IncrementalLoadingCollection_UsesSourceHasMoreItemsForEmptyPage()
    {
        var source = new ControlledIncrementalSource<int>();
        var collection = new IncrementalLoadingCollection<int>(source, 3);

        var first = collection.LoadMoreItemsAsync(0);
        var firstCall = await source.WaitForCallAsync(0);
        firstCall.Completion.SetResult([]);

        Assert.AreEqual(0, await first);
        Assert.IsTrue(collection.HasMoreItems);

        source.HasMoreItems = false;
        var second = collection.LoadMoreItemsAsync(0);
        var secondCall = await source.WaitForCallAsync(1);
        secondCall.Completion.SetResult([]);

        Assert.AreEqual(0, await second);
        Assert.IsFalse(collection.HasMoreItems);
    }

    [TestMethod]
    public async Task Test_IncrementalLoadingCollection_DisposeCancelsLoadAndSource()
    {
        var source = new ControlledIncrementalSource<int>();
        var collection = new IncrementalLoadingCollection<int>(source, 3);

        var loading = collection.LoadMoreItemsAsync(0);
        _ = await source.WaitForCallAsync(0);

        collection.Dispose();

        await AssertOperationCanceledAsync(loading);
        Assert.IsTrue(source.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => collection.LoadMoreItemsAsync(0));
    }

    [TestMethod]
    public void Test_FilteredIncrementalCollection_PassThroughWithoutFilter()
    {
        var source = new ControlledIncrementalSource<string>();
        var inner = new IncrementalLoadingCollection<string>(source, 10);
        using var filtered = new FilteredIncrementalCollection<string>(inner);

        inner.Add("apple");
        inner.Add("banana");
        inner.Add("cherry");

        Assert.AreEqual(3, filtered.Count);
        Assert.AreEqual("apple", filtered[0]);
        Assert.AreEqual("banana", filtered[1]);
        Assert.AreEqual("cherry", filtered[2]);
    }

    [TestMethod]
    public void Test_FilteredIncrementalCollection_FiltersCorrectly()
    {
        var source = new ControlledIncrementalSource<string>();
        var inner = new IncrementalLoadingCollection<string>(source, 10);
        using var filtered = new FilteredIncrementalCollection<string>(inner)
        {
            Filter = obj => obj is string s && s.StartsWith('b')
        };

        inner.Add("apple");
        inner.Add("banana");
        inner.Add("blueberry");
        inner.Add("cherry");

        Assert.AreEqual(2, filtered.Count);
        Assert.AreEqual("banana", filtered[0]);
        Assert.AreEqual("blueberry", filtered[1]);

        // Removing a filtered item should update view
        inner.Remove("banana");
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("blueberry", filtered[0]);

        // Removing a non-matching item should not affect view
        inner.Remove("apple");
        Assert.AreEqual(1, filtered.Count);
    }

    [TestMethod]
    public void Test_FilteredIncrementalCollection_SortsCorrectly()
    {
        var source = new ControlledIncrementalSource<string>();
        var inner = new IncrementalLoadingCollection<string>(source, 10);
        using var filtered = new FilteredIncrementalCollection<string>(inner)
        {
            Comparer = Comparer<object>.Create((a, b) => string.Compare((string) a, (string) b, StringComparison.Ordinal))
        };

        inner.Add("cherry");
        inner.Add("apple");
        inner.Add("date");
        inner.Add("banana");

        Assert.AreEqual(4, filtered.Count);
        Assert.AreEqual("apple", filtered[0]);
        Assert.AreEqual("banana", filtered[1]);
        Assert.AreEqual("cherry", filtered[2]);
        Assert.AreEqual("date", filtered[3]);
    }

    [TestMethod]
    public void Test_FilteredIncrementalCollection_DynamicFilterAndComparerUpdate()
    {
        var source = new ControlledIncrementalSource<string>();
        var inner = new IncrementalLoadingCollection<string>(source, 10);
        using var filtered = new FilteredIncrementalCollection<string>(inner);

        inner.Add("banana");
        inner.Add("apple");
        inner.Add("blueberry");
        inner.Add("cherry");

        Assert.AreEqual(4, filtered.Count);

        // Apply filter dynamically
        filtered.Filter = obj => obj is string s && s.Contains('a');
        Assert.AreEqual(2, filtered.Count); // "banana", "apple"

        // Apply descending sort dynamically
        filtered.Comparer = Comparer<object>.Create((a, b) => string.Compare((string) b, (string) a, StringComparison.Ordinal));
        Assert.AreEqual(2, filtered.Count);
        Assert.AreEqual("banana", filtered[0]);
        Assert.AreEqual("apple", filtered[1]);

        // Clear filter
        filtered.Filter = null;
        Assert.AreEqual(4, filtered.Count);
        Assert.AreEqual("cherry", filtered[0]);
        Assert.AreEqual("blueberry", filtered[1]);
        Assert.AreEqual("banana", filtered[2]);
        Assert.AreEqual("apple", filtered[3]);
    }
}
