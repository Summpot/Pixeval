// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Pixeval.Pixiv;
using Pixeval.Mako;

namespace Pixeval.ViewModels.Works;

public sealed class SeriesViewViewModel : EntryViewViewModel<Series, Series>
{
    public SeriesViewViewModel() : this(null)
    {
    }

    public SeriesViewViewModel(IBlockedContentService? blockedContent) : base(blockedContent)
    {
    }

    public void ResetSource(IAsyncEnumerable<Series> source, SimpleWorkType workType) =>
        base.ResetSource(source, (series, _) => series with { WorkType = workType });
}
