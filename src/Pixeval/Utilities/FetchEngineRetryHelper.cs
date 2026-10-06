// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Pixeval.Utilities;

public static class FetchEngineRetryHelper
{
    private static readonly TimeSpan[] _RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1)
    ];

    public static IAsyncEnumerable<TResult> StreamAsync<TResult>(
        IAsyncEnumerable<TResult> engine,
        Func<int, TimeSpan>? retryDelayProvider = null,
        bool cancelEngineOnCancellation = true,
        CancellationToken token = default) =>
        StreamCoreAsync(engine, retryDelayProvider ?? GetRetryDelay, cancelEngineOnCancellation, token);

    public static async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        Func<int, TimeSpan>? retryDelayProvider = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        retryDelayProvider ??= GetRetryDelay;
        var retryCount = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                return await operation(token).ConfigureAwait(false);
            }
            catch (Exception exception) when (!token.IsCancellationRequested && IsRetryable(exception))
            {
                var delay = retryDelayProvider(retryCount++);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }
    }

    private static async IAsyncEnumerable<TResult> StreamCoreAsync<TResult>(
        IAsyncEnumerable<TResult> engine,
        Func<int, TimeSpan> retryDelayProvider,
        bool cancelEngineOnCancellation,
        [EnumeratorCancellation] CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(retryDelayProvider);

        if (token.IsCancellationRequested)
        {
            if (cancelEngineOnCancellation && engine is IFetchEngine<TResult> fetchEngine)
                fetchEngine.EngineHandle.Cancel();
            token.ThrowIfCancellationRequested();
        }

        await using var enumerator = engine.GetAsyncEnumerator(token);
        var retryCount = 0;
        while (true)
        {
            if (token.IsCancellationRequested)
            {
                if (cancelEngineOnCancellation && engine is IFetchEngine<TResult> fetchEngine)
                    fetchEngine.EngineHandle.Cancel();
                token.ThrowIfCancellationRequested();
            }

            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (!token.IsCancellationRequested && IsRetryable(exception))
            {
                var delay = retryDelayProvider(retryCount++);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, token).ConfigureAwait(false);
                continue;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (cancelEngineOnCancellation && engine is IFetchEngine<TResult> fetchEngine)
                    fetchEngine.EngineHandle.Cancel();
                throw;
            }

            if (hasNext)
            {
                retryCount = 0;
                yield return enumerator.Current;
            }
            else
            {
                if (engine is IFetchEngine<TResult> fetchEngine)
                    fetchEngine.EngineHandle.IsCompleted = true;
                yield break;
            }
        }
    }

    private static TimeSpan GetRetryDelay(int retryCount) =>
        _RetryDelays[Math.Min(retryCount, _RetryDelays.Length - 1)];

    private static bool IsRetryable(Exception exception) =>
        exception is HttpRequestException or TimeoutException or OperationCanceledException;
}
