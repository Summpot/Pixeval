// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Controls;
using Pixeval.Native.Mako;

namespace Pixeval.ViewModels;

public class SpotlightItemViewModel(SpotlightArticle spotlight) : ThumbnailEntryViewModel<SpotlightArticle>(spotlight),
    IFactory<SpotlightArticle, SpotlightItemViewModel>
{
    public static SpotlightItemViewModel CreateInstance(SpotlightArticle entry) => new(entry);

    public override string ThumbnailUrl => Entry.Thumbnail;

    public override Uri AppUri => Entry.AppUri;

    public override Uri WebsiteUri => Entry.WebsiteUri;
}
