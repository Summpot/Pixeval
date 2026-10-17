// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Mako;

namespace Pixeval.Pixiv;

public interface IWorkEntry
{
    long Id { get; }
    User User { get; }
    string Title { get; }
    Series? Series => null;
}



public enum DomainFrontingType
{
    Fragmentation,
    Desync,
    Ech
}

public static class MakoHttpOptions
{
    public const string AppApiHost = "app-api.pixiv.net";
    public const string WebApiHost = "www.pixiv.net";
    public const string AccountHost = "accounts.pixiv.net";
    public const string OAuthHost = "oauth.secure.pixiv.net";
    public const string ImageHost = "i.pximg.net";
    public const string ImageHost2 = "s.pximg.net";
}

public class RateLimitEventArgs(DateTimeOffset retryAt) : EventArgs
{
    public DateTimeOffset RetryAt { get; } = retryAt;
}

public static class FetchEngineExtensions
{
    public static IAsyncEnumerable<object> AsObjects<T>(this IAsyncEnumerable<T> source) =>
        source is IPageFeed<T> feed
            ? new ObjectPageFeed<T>(source, feed)
            : System.Linq.AsyncEnumerable.Select(source, static item => ArtworkPayloads.Unwrap(item!));

    private sealed class ObjectPageFeed<T>(IAsyncEnumerable<T> source, IPageFeed<T> feed) : IAsyncEnumerable<object>, IPageFeed<object>
    {
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            System.Linq.AsyncEnumerable.Select(source, static item => ArtworkPayloads.Unwrap(item!)).GetAsyncEnumerator(cancellationToken);

        public async Task<(IReadOnlyList<object> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
        {
            var (items, exhausted) = await feed.FetchPageAsync(cancellationToken).ConfigureAwait(false);
            return (items.Select(static item => ArtworkPayloads.Unwrap(item!)).ToArray(), exhausted);
        }

        public void Cancel() => feed.Cancel();
    }
}
