using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.Services.Network;

namespace Pixeval.Tests;

[TestClass]
public sealed class FetchEngineRetryHelperTest
{
    [TestMethod]
    public async Task StreamAsync_RetriesInterruptedEnumeratorWithoutStartingOver()
    {
        var engine = new InterruptingEnumerable();
        var results = new List<int>();

        await foreach (var result in FetchEngineRetryHelper.StreamAsync(
                           engine,
                           static _ => TimeSpan.Zero))
            results.Add(result);

        Assert.AreSequenceEqual([1, 2], results);
        Assert.AreEqual(1, engine.EnumeratorCount);
        Assert.AreEqual(4, engine.MoveNextCount);
    }

    [TestMethod]
    public async Task StreamAsync_CanCancelEnumerationWithoutCancellingFeed()
    {
        var feed = new CancellableFeed();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in FetchEngineRetryHelper.StreamAsync(
                               feed,
                               cancelEngineOnCancellation: false,
                               token: cancellationTokenSource.Token))
            {
            }
        });

        Assert.IsFalse(feed.Cancelled);
        Assert.AreEqual(0, feed.FetchCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_RetriesRateLimitedRequest()
    {
        var attemptCount = 0;

        var result = await FetchEngineRetryHelper.ExecuteAsync(
            _ => ++attemptCount is 1
                ? Task.FromException<int>(new HttpRequestException("test"))
                : Task.FromResult(42),
            static _ => TimeSpan.Zero);

        Assert.AreEqual(42, result);
        Assert.AreEqual(2, attemptCount);
    }

    private sealed class InterruptingEnumerable : IAsyncEnumerable<int>
    {
        public int EnumeratorCount { get; private set; }

        public int MoveNextCount { get; private set; }

        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            EnumeratorCount++;
            return new Enumerator(this);
        }

        private sealed class Enumerator(InterruptingEnumerable engine) : IAsyncEnumerator<int>
        {
            public int Current { get; private set; }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public ValueTask<bool> MoveNextAsync()
            {
                engine.MoveNextCount++;
                switch (engine.MoveNextCount)
                {
                    case 1:
                        Current = 1;
                        return ValueTask.FromResult(true);
                    case 2:
                        throw new HttpRequestException("interrupted");
                    case 3:
                        Current = 2;
                        return ValueTask.FromResult(true);
                    default:
                        return ValueTask.FromResult(false);
                }
            }
        }
    }

    private sealed class CancellableFeed : IPageFeed<int>, IAsyncEnumerable<int>
    {
        public bool Cancelled { get; private set; }

        public int FetchCount { get; private set; }

        public Task<(IReadOnlyList<int> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
        {
            FetchCount++;
            return Task.FromResult<(IReadOnlyList<int>, bool)>(([], true));
        }

        public void Cancel() => Cancelled = true;

        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
