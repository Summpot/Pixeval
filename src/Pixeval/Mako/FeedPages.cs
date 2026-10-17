// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pixeval.Mako;

public partial class IllustrationFeed : IPageFeed<Illustration>
{
    public async Task<(IReadOnlyList<Illustration> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class NovelFeed : IPageFeed<Novel>
{
    public async Task<(IReadOnlyList<Novel> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class WorkFeed : IPageFeed<WorkEntry>
{
    public async Task<(IReadOnlyList<WorkEntry> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class UserFeed : IPageFeed<User>
{
    public async Task<(IReadOnlyList<User> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class SeriesFeed : IPageFeed<Series>
{
    public async Task<(IReadOnlyList<Series> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class SpotlightFeed : IPageFeed<SpotlightArticle>
{
    public async Task<(IReadOnlyList<SpotlightArticle> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}

public partial class CommentFeed : IPageFeed<CommentRecord>
{
    public async Task<(IReadOnlyList<CommentRecord> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default)
    {
        var page = await NextPageAsync(cancellationToken).ConfigureAwait(false);
        return (page.Items, page.Exhausted);
    }
}
