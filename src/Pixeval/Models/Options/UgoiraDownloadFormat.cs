// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Models.Options;

[LocalizationMetadata]
public enum UgoiraDownloadFormat
{
    [LocalizedResource(EnumResources.UgoiraDownloadFormat.Original)]
    Original,

    [LocalizedResource(EnumResources.UgoiraDownloadFormat.Gif)]
    Gif,

    [LocalizedResource(EnumResources.UgoiraDownloadFormat.Apng)]
    Apng,

    [LocalizedResource(EnumResources.UgoiraDownloadFormat.Webp)]
    Webp,

    [LocalizedResource(EnumResources.UgoiraDownloadFormat.Mp4)]
    Mp4
}
