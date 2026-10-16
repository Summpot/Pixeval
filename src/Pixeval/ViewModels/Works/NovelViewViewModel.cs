// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Mako;

namespace Pixeval.ViewModels.Works;

public sealed class NovelViewViewModel : WorkViewViewModelBase<Mako.Novel, Mako.Novel>
{
    public NovelViewViewModel(AppSettings appSettings, IBlockedContentService? blockedContent = null)
        : base(appSettings, blockedContent)
    {
    }

    public override bool RequireAdaptiveGrid => true;
}
