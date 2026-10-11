// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Config;

public record DownloadFormatsSettings
{
    public string IllustrationDownloadFormat { get; set; } = Download.IllustrationDownloadFormatToken.DefaultToken;

    public string UgoiraDownloadFormat { get; set; } = Download.UgoiraDownloadFormatToken.DefaultToken;

    public string NovelDownloadFormat { get; set; } = Download.NovelDownloadFormatToken.DefaultToken;
}
