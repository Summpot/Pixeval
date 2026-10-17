// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

[LocalizationMetadata]
public enum WorkSubscriptionType
{
    [LocalizedResource(nameof(Strings.EnumWorkSubscriptionTypeBookmarks))]
    Bookmarks,

    [LocalizedResource(nameof(Strings.EnumWorkSubscriptionTypePosts))]
    Posts,

    [LocalizedResource(nameof(Strings.EnumWorkSubscriptionTypeSeries))]
    Series
}
