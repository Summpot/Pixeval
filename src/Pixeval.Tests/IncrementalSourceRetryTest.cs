using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Collections;
using Pixeval.Models.Pixiv;
using Pixeval.ViewModels;

namespace Pixeval.Tests;

[TestClass]
public sealed class IncrementalSourceRetryTest
{
    private sealed record TestItem(string Id);

    [TestMethod]
    public async Task InterruptedPageStopsAndResumesTheSameEnumerator()
    {
        var engine = new InterruptedEngine();
        using var source = new IncrementalSource<TestItem, TestItem>(engine, static (entry, _) => entry);
        using var collection = new IncrementalLoadingCollection<TestItem>(source);

        Assert.AreEqual(1, await collection.LoadMoreItemsAsync(0));
        Assert.AreEqual(2, engine.MoveNextCount);
        Assert.IsTrue(collection.IsInterrupted);
        Assert.IsTrue(collection.HasMoreItems);

        Assert.AreEqual(1, await collection.LoadMoreItemsAsync(0));
        Assert.AreEqual(4, engine.MoveNextCount);
        Assert.AreEqual(1, engine.EnumeratorCount);
        Assert.AreSequenceEqual(new[] { "1", "2" }, new[] { collection[0].Id, collection[1].Id });
        Assert.IsFalse(collection.IsInterrupted);
        Assert.IsFalse(collection.HasMoreItems);
    }

    private sealed class InterruptedEngine : IFetchEngine<TestItem>
    {
        public IFetchEngineHandle EngineHandle { get; } = new DummyEngineHandle();
        public int EnumeratorCount { get; private set; }
        public int MoveNextCount { get; private set; }

        public IAsyncEnumerator<TestItem> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            EnumeratorCount++;
            return new Enumerator(this);
        }

        private sealed class Enumerator(InterruptedEngine engine) : IAsyncEnumerator<TestItem>
        {
            public TestItem Current { get; private set; } = null!;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            public ValueTask<bool> MoveNextAsync()
            {
                switch (++engine.MoveNextCount)
                {
                    case 1:
                        Current = new TestItem("1");
                        return ValueTask.FromResult(true);
                    case 2:
                        return ValueTask.FromResult(false);
                    case 3:
                        Current = new TestItem("2");
                        return ValueTask.FromResult(true);
                    default:
                        engine.EngineHandle.IsCompleted = true;
                        return ValueTask.FromResult(false);
                }
            }
        }
    }
}
