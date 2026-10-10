// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Models.Download;
using Pixeval.Models.Options;

namespace Pixeval.Services;

public interface IDownloadFormatService
{
    UgoiraDownloadFormatToken GetAvailableUgoiraDownloadFormatToken(string? ugoiraDownloadFormat = null);

    IllustrationDownloadFormatToken GetAvailableIllustrationDownloadFormatToken(string? illustrationDownloadFormat = null);

    NovelDownloadFormatToken GetAvailableNovelDownloadFormatToken(string? novelDownloadFormat = null);

    string? GetUgoiraExtension(string? ugoiraDownloadFormat = null);

    string? GetIllustrationExtension(string? illustrationDownloadFormat = null);

    string GetNovelExtension(string? novelDownloadFormat = null);

    string GetNovelExtension(NovelDownloadFormatToken token);

    NovelDownloadFormat GetNovelFormat(string extension);

    bool TryGetNovelFormat(string extension, out NovelDownloadFormat format);
}
