// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

[LocalizationMetadata]
public enum NovelDownloadFormat
{
    [LocalizedResource(EnumResources.NovelDownloadFormat.Html)]
    Html,

    [LocalizedResource(EnumResources.NovelDownloadFormat.Md)]
    Md,

    [LocalizedResource(EnumResources.NovelDownloadFormat.OriginalTxt)]
    OriginalTxt,

    [LocalizedResource(EnumResources.NovelDownloadFormat.Epub)]
    Epub
}
