// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Series = Pixeval.Native.Mako.Series;
using SeriesViewDataProvider = Pixeval.ViewModels.SharableViewDataProvider<
    Pixeval.Native.Mako.Series,
    Pixeval.Native.Mako.Series>;

namespace Pixeval.ViewModels;

public sealed class SeriesViewViewModel
    : EntryViewViewModel<Series, Series>, IRefCloneable<SeriesViewViewModel>
{
    public SeriesViewViewModel() : this(new SeriesViewDataProvider())
    {
    }

    private SeriesViewViewModel(SeriesViewDataProvider dataProvider)
    {
        DataProvider = dataProvider;
    }

    public override SeriesViewDataProvider DataProvider { get; }

    public void ResetEngine(IFetchEngine<Series> fetchEngine, SimpleWorkType workType) =>
        base.ResetEngine(fetchEngine, (series, _) =>
        {
            series.WorkType = workType;
            return series;
        });

    public SeriesViewViewModel CloneRef() => new(DataProvider.CloneRef());
}
