// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Frozen;
using IllustrationViewDataProvider = Pixeval.ViewModels.SharableViewDataProvider<
    object,
    object>;

namespace Pixeval.ViewModels;

public sealed class IllustrationViewViewModel
    : WorkViewViewModelBase<object, object>, IRefCloneable<IllustrationViewViewModel>
{
    public IllustrationViewViewModel() : this(new IllustrationViewDataProvider(), null)
    {
    }

    private IllustrationViewViewModel(IllustrationViewDataProvider dataProvider, FrozenSet<string>? blockedTags) : base(blockedTags)
    {
        DataProvider = dataProvider;
        SetFilters();
    }

    public override IllustrationViewDataProvider DataProvider { get; }

    public override bool RequireAdaptiveGrid => false;

    public IllustrationViewViewModel CloneRef() => new(DataProvider.CloneRef(), CachedBlockedTags);
}
