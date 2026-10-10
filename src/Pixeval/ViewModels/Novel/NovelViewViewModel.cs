// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Frozen;
using Pixeval.Native.Mako;

namespace Pixeval.ViewModels;

public sealed class NovelViewViewModel : WorkViewViewModelBase<Novel, Novel>
{
    public NovelViewViewModel() : this(null)
    {
    }

    public NovelViewViewModel(FrozenSet<string>? blockedTags) : base(blockedTags)
    {
    }

    public override bool RequireAdaptiveGrid => true;
}
