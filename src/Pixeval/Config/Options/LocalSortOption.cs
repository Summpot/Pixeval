// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using FluentIcons.Common;
using Pixeval.Attributes;

namespace Pixeval.Config;

public enum LocalSortOption
{
    [LocalizedResource(Symbol.ArrowSort, nameof(Strings.EnumLocalSortOptionDoNotSort))]
    DoNotSort,

    [LocalizedResource(Symbol.ArrowTrendingSparkle, nameof(Strings.EnumLocalSortOptionPopularityDescending))]
    PopularityDescending,

    [LocalizedResource(Symbol.ArrowSortDownLines, nameof(Strings.EnumLocalSortOptionPublishDateDescending))]
    PublishDateDescending,

    [LocalizedResource(Symbol.ArrowSortUpLines, nameof(Strings.EnumLocalSortOptionPublishDateAscending))]
    PublishDateAscending
}
