// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Frozen;
using Pixeval.Mako;

namespace Pixeval.ViewModels.Works;

public sealed class NovelViewViewModel : WorkViewViewModelBase<Mako.Novel, Mako.Novel>
{
    public NovelViewViewModel() : this(null)
    {
    }

    public NovelViewViewModel(FrozenSet<string>? blockedTags) : base(blockedTags)
    {
    }

    public override bool RequireAdaptiveGrid => true;
}
