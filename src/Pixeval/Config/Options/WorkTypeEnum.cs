// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

[LocalizationMetadata]
public enum WorkTypeEnum
{
    [LocalizedResource(nameof(Strings.EnumWorkTypeEnumIllustration))]
    Illustration,

    [LocalizedResource(nameof(Strings.EnumWorkTypeEnumManga))]
    Manga,

    [LocalizedResource(nameof(Strings.EnumWorkTypeEnumUgoira))]
    Ugoira,

    [LocalizedResource(nameof(Strings.EnumWorkTypeEnumNovel))]
    Novel
}
