// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Frozen;

namespace Pixeval.ViewModels;

public sealed class IllustrationViewViewModel : WorkViewViewModelBase<object, object>
{
    public IllustrationViewViewModel() : this(null)
    {
    }

    public IllustrationViewViewModel(FrozenSet<string>? blockedTags) : base(blockedTags)
    {
    }

    public override bool RequireAdaptiveGrid => false;
}
