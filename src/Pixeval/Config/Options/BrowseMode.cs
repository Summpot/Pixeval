// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

public enum BrowseMode
{
    [LocalizedResource(nameof(Strings.EnumBrowseModeSwipe))]
    Swipe,

    [LocalizedResource(nameof(Strings.EnumBrowseModeContinuous))]
    Continuous
}
