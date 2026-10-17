// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.ViewModels.Works;

public sealed class IllustrationViewViewModel : WorkViewViewModelBase<object, object>
{
    public IllustrationViewViewModel(AppSettings appSettings, IBlockedContentService? blockedContent = null)
        : base(appSettings, blockedContent)
    {
    }

    public override bool RequireAdaptiveGrid => false;
}
