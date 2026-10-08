// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using SpotlightViewDataProvider = Pixeval.ViewModels.SharableViewDataProvider<
    Pixeval.Native.Mako.SpotlightArticle,
    Pixeval.Native.Mako.SpotlightArticle>;

namespace Pixeval.ViewModels;

public sealed class SpotlightViewViewModel
    : EntryViewViewModel<Pixeval.Native.Mako.SpotlightArticle, Pixeval.Native.Mako.SpotlightArticle>, IRefCloneable<SpotlightViewViewModel>
{
    public SpotlightViewViewModel() : this(new SpotlightViewDataProvider())
    {
    }

    private SpotlightViewViewModel(SpotlightViewDataProvider dataProvider)
    {
        DataProvider = dataProvider;
    }

    public override SpotlightViewDataProvider DataProvider { get; }

    public SpotlightViewViewModel CloneRef() => new(DataProvider.CloneRef());
}
