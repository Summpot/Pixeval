// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

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

    public void ResetEngine(IFetchEngine<Series> fetchEngine, SimpleWorkType workType) =>
        base.ResetEngine(fetchEngine, (series, _) => series with { WorkType = workType });
}
