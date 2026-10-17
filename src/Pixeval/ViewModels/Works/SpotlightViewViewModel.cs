// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Mako;

namespace Pixeval.ViewModels.Works;

public sealed class SpotlightViewViewModel : EntryViewViewModel<SpotlightArticle, SpotlightArticle>
{
    public SpotlightViewViewModel() : this(null)
    {
    }

    public SpotlightViewViewModel(IBlockedContentService? blockedContent) : base(blockedContent)
    {
    }
}
