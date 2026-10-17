// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pixeval.Mako;

/// <summary>
/// Native fetch objects expose page-sized results. A failed page is an exception and does not exhaust the feed.
/// </summary>
public interface IPageFeed<T>
{
    Task<(IReadOnlyList<T> Items, bool Exhausted)> FetchPageAsync(CancellationToken cancellationToken = default);

    void Cancel();
}
