// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Download;
using Pixeval.Models.Extensions;
using Pixeval.Models.Options;
using Pixeval.Native.Media;

namespace Pixeval.Services;

public sealed class DownloadFormatService : IDownloadFormatService
{
    private readonly AppSettings _appSettings;
    private readonly ExtensionService _extensionService;

    public DownloadFormatService(AppSettings appSettings, ExtensionService extensionService)
    {
        _appSettings = appSettings;
        _extensionService = extensionService;
    }

    public UgoiraDownloadFormatToken GetAvailableUgoiraDownloadFormatToken(string? ugoiraDownloadFormat = null)
    {
        ugoiraDownloadFormat ??= _appSettings.DownloadSettings.DownloadFormats.UgoiraDownloadFormat;
        var token = new UgoiraDownloadFormatToken(ugoiraDownloadFormat);
        if (token.BuiltInFormat is not null)
        {
            if (token.BuiltInFormat == UgoiraDownloadFormat.Mp4 && !MediaEngine.IsMp4Available)
                return UgoiraDownloadFormatToken.Default;
            return token;
        }

        if (token.ExtensionFormatExtension is { } extension
            && _extensionService.GetAnimatedImageFormatProvider(extension) is not null)
            return token;

        return UgoiraDownloadFormatToken.Default;
    }

    public IllustrationDownloadFormatToken GetAvailableIllustrationDownloadFormatToken(string? illustrationDownloadFormat = null)
    {
        illustrationDownloadFormat ??= _appSettings.DownloadSettings.DownloadFormats.IllustrationDownloadFormat;
        var token = new IllustrationDownloadFormatToken(illustrationDownloadFormat);
        if (token.BuiltInFormat is IllustrationDownloadFormat.Original)
            return token;

        if (token.ExtensionFormatExtension is { } extension
            && _extensionService.GetStaticImageFormatProvider(extension) is not null)
            return token;

        return IllustrationDownloadFormatToken.Default;
    }

    public NovelDownloadFormatToken GetAvailableNovelDownloadFormatToken(string? novelDownloadFormat = null)
    {
        novelDownloadFormat ??= _appSettings.DownloadSettings.DownloadFormats.NovelDownloadFormat;
        var token = new NovelDownloadFormatToken(novelDownloadFormat);
        if (token.BuiltInFormat is not null)
            return token;

        if (token.ExtensionFormatExtension is { } extension
            && _extensionService.GetNovelFormatProvider(extension) is not null)
            return token;

        return NovelDownloadFormatToken.Default;
    }

    public string? GetUgoiraExtension(string? ugoiraDownloadFormat = null)
    {
        var token = GetAvailableUgoiraDownloadFormatToken(ugoiraDownloadFormat);
        if (token.ExtensionFormatExtension is { } extension)
            return extension;

        return (token.BuiltInFormat ?? UgoiraDownloadFormatToken.DefaultBuiltInFormat) switch
        {
            UgoiraDownloadFormat.Original => null,
            UgoiraDownloadFormat.Gif => "gif",
            UgoiraDownloadFormat.Apng => "png",
            UgoiraDownloadFormat.Webp => "webp",
            UgoiraDownloadFormat.Mp4 => "mp4",
            _ => throw new ArgumentOutOfRangeException(nameof(ugoiraDownloadFormat))
        };
    }

    public string? GetIllustrationExtension(string? illustrationDownloadFormat = null)
    {
        var token = GetAvailableIllustrationDownloadFormatToken(illustrationDownloadFormat);
        if (token.ExtensionFormatExtension is { } extension)
            return extension;

        return (token.BuiltInFormat ?? IllustrationDownloadFormatToken.DefaultBuiltInFormat) switch
        {
            IllustrationDownloadFormat.Original => null,
            _ => throw new ArgumentOutOfRangeException(nameof(illustrationDownloadFormat))
        };
    }

    public string GetNovelExtension(string? novelDownloadFormat = null) =>
        GetNovelExtension(GetAvailableNovelDownloadFormatToken(novelDownloadFormat));

    public string GetNovelExtension(NovelDownloadFormatToken token)
    {
        if (token.ExtensionFormatExtension is { } extension)
            return extension;

        return (token.BuiltInFormat ?? NovelDownloadFormatToken.DefaultBuiltInFormat) switch
        {
            NovelDownloadFormat.OriginalTxt => "txt",
            NovelDownloadFormat.Html => "html",
            NovelDownloadFormat.Md => "md",
            NovelDownloadFormat.Epub => "epub",
            _ => throw new ArgumentOutOfRangeException(nameof(token))
        };
    }

    public NovelDownloadFormat GetNovelFormat(string extension)
    {
        if (TryGetNovelFormat(extension, out var format))
            return format;

        throw new ArgumentOutOfRangeException(nameof(extension));
    }

    public bool TryGetNovelFormat(string extension, out NovelDownloadFormat format)
    {
        return extension switch
        {
            ".txt" => Assign(NovelDownloadFormat.OriginalTxt, out format),
            ".html" => Assign(NovelDownloadFormat.Html, out format),
            ".md" => Assign(NovelDownloadFormat.Md, out format),
            ".epub" => Assign(NovelDownloadFormat.Epub, out format),
            _ => Assign(default, out format, false)
        };

        static bool Assign(NovelDownloadFormat value, out NovelDownloadFormat format, bool result = true)
        {
            format = value;
            return result;
        }
    }
}
