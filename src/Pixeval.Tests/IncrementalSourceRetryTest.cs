using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Common.Collections;
using Pixeval.Mako;
using Pixeval.ViewModels.Works;

namespace Pixeval.Tests;

[TestClass]
public sealed class IncrementalSourceRetryTest
{
    private sealed record TestItem(string Id);

    [TestMethod]
    public async Task InterruptedPageStopsAndResumesTheSameFeed()
    {
        var feed = new InterruptedFeed();
        using var source = new IncrementalSource<TestItem, TestItem>(feed, static (entry, _) => entry);
        using var collection = new IncrementalLoadingCollection<TestItem>(source);

        Assert.AreEqual(0, await collection.LoadMoreItemsAsync(0));
        Assert.AreEqual(1, feed.FetchCount);
        Assert.IsTrue(collection.IsInterrupted);
        Assert.IsTrue(collection.HasMoreItems);
        Assert.IsNotNull(collection.LoadError);

        Assert.AreEqual(1, await collection.LoadMoreItemsAsync(0));
        Assert.AreEqual(2, feed.FetchCount);
        Assert.AreSequenceEqual(new[] { "1" }, new[] { collection[0].Id });
        Assert.IsFalse(collection.IsInterrupted);
        Assert.IsFalse(collection.HasMoreItems);
    }

    [TestMethod]
    public async Task ItemEnumerableEndIsNotInterrupted()
    {
        using var source = new IncrementalSource<TestItem, TestItem>(Single(), static (entry, _) => entry);
        using var collection = new IncrementalLoadingCollection<TestItem>(source);

        Assert.AreEqual(1, await collection.LoadMoreItemsAsync(0));
        Assert.IsFalse(collection.IsInterrupted);
        Assert.IsFalse(collection.HasMoreItems);
    }

    private static async IAsyncEnumerable<TestItem> Single()
    {
        await Task.Yield();
        yield return new TestItem("1");
    }

    private sealed class InterruptedFeed : IPageFeed<TestItem>, IAsyncEnumerable<TestItem>
    {
        public int FetchCount { get; private set; }

        public Task<(IReadOnlyList<TestItem> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
        {
            FetchCount++;
            if (FetchCount is 1)
                return Task.FromException<(IReadOnlyList<TestItem>, bool)>(new HttpRequestException("interrupted"));
            return Task.FromResult<(IReadOnlyList<TestItem>, bool)>(([new TestItem("1")], true));
        }

        public void Cancel()
        {
        }

        public IAsyncEnumerator<TestItem> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
