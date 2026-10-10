// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;

namespace Pixeval.ViewModels;

public sealed class SeriesViewViewModel : EntryViewViewModel<Series, Series>
{
    public void ResetEngine(IFetchEngine<Series> fetchEngine, SimpleWorkType workType) =>
        base.ResetEngine(fetchEngine, (series, _) => series with { WorkType = workType });
}
