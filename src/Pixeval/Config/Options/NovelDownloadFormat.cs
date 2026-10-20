// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

public enum NovelDownloadFormat
{
    [LocalizedResource(nameof(Strings.EnumNovelDownloadFormatHtml))]
    Html,

    [LocalizedResource(nameof(Strings.EnumNovelDownloadFormatMd))]
    Md,

    [LocalizedResource(nameof(Strings.EnumNovelDownloadFormatOriginalTxt))]
    OriginalTxt,

    [LocalizedResource(nameof(Strings.EnumNovelDownloadFormatEpub))]
    Epub
}
