// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

[LocalizationMetadata]
public enum UgoiraDownloadFormat
{
    [LocalizedResource(nameof(Strings.EnumUgoiraDownloadFormatOriginal))]
    Original,

    [LocalizedResource(nameof(Strings.EnumUgoiraDownloadFormatGif))]
    Gif,

    [LocalizedResource(nameof(Strings.EnumUgoiraDownloadFormatApng))]
    Apng,

    [LocalizedResource(nameof(Strings.EnumUgoiraDownloadFormatWebp))]
    Webp,

    [LocalizedResource(nameof(Strings.EnumUgoiraDownloadFormatMp4))]
    Mp4
}
