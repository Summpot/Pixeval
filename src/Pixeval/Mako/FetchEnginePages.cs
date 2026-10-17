// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pixeval.Mako;

public partial class IllustrationFetchEngine : IPageFeed<Illustration>
{
    public async Task<(IReadOnlyList<Illustration> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class NovelFetchEngine : IPageFeed<Novel>
{
    public async Task<(IReadOnlyList<Novel> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class WorkFetchEngine : IPageFeed<WorkEntry>
{
    public async Task<(IReadOnlyList<WorkEntry> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class UserFetchEngine : IPageFeed<User>
{
    public async Task<(IReadOnlyList<User> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class SeriesFetchEngine : IPageFeed<Series>
{
    public async Task<(IReadOnlyList<Series> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class SpotlightFetchEngine : IPageFeed<SpotlightArticle>
{
    public async Task<(IReadOnlyList<SpotlightArticle> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class CommentFetchEngine : IPageFeed<CommentRecord>
{
    public async Task<(IReadOnlyList<CommentRecord> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}
