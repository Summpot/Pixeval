// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

public enum WorkSubscriptionWorkKind
{
    [LocalizedResource(nameof(Strings.EnumWorkSubscriptionWorkKindIllustration))]
    Illustration,

    [LocalizedResource(nameof(Strings.EnumWorkSubscriptionWorkKindManga))]
    Manga,

    [LocalizedResource(nameof(Strings.EnumWorkSubscriptionWorkKindNovel))]
    Novel
}
