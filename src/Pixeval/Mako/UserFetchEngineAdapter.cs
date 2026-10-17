// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Pixiv;

namespace Pixeval.Mako;

public static class UserFetchEngineExtensions
{
    public static IFetchEngine<User> ToFetchEngine(this UserFetchEngine source) => new UserFetchEngineAdapter(source);
}

// The generated async iterator ends permanently on null, even when native paging was interrupted.
internal sealed class UserFetchEngineAdapter(UserFetchEngine source) : IFetchEngine<User>, IFetchEngineHandle
{
    public IFetchEngineHandle EngineHandle => this;
    public bool IsCompleted { get => source.IsCompleted(); set { } }
    public bool IsCancelled { get; private set; }

    public void Cancel()
    {
        IsCancelled = true;
        source.Cancel();
    }

    public IAsyncEnumerator<User> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new Enumerator(this, source, cancellationToken);

    private sealed class Enumerator(UserFetchEngineAdapter owner, UserFetchEngine source, CancellationToken token) : IAsyncEnumerator<User>
    {
        public User Current { get; private set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            token.ThrowIfCancellationRequested();
            if (owner.IsCancelled)
                return false;
            var item = await source.NextAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (owner.IsCancelled)
                return false;
            if (item is { } user)
            {
                Current = user;
                return true;
            }

            if (source.LastError() is { } error)
                throw new HttpRequestException(error);
            return false;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
