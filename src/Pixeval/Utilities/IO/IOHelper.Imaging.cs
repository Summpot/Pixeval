// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models.Download;
using Pixeval.Models.Extensions;
using Pixeval.Models.Options;
using Pixeval.Native.Media;

using Pixeval.Services;

namespace Pixeval.Utilities.IO;

public static partial class IoHelper
{
    public const string PixevalTempExtension = ".pixevaldownloading";

    private const string FileExtensionTokenPrefix = "<ext";

    private static IDownloadFormatService? DownloadFormatService => App.Services?.GetService<IDownloadFormatService>();

    extension(Stream stream)
    {
        public async Task StreamSaveToFileAsync(string path)
        {
            await using var fileStream = FileHelper.CreateAsyncWriteCreateParent(path);
            await stream.CopyToAsync(fileStream);
        }

        public async Task StreamsCompressSaveToFileAsync(string path)
        {
            await using var fileStream = FileHelper.CreateAsyncWriteCreateParent(path);
            await stream.CopyToAsync(fileStream);
        }

        public async Task<Bitmap> DecodeBitmapImageAsync(bool disposeOfImageStream, int? desiredWidth = null)
        {
            var bitmapImage = await Task.Run(() => desiredWidth is { } w ? Bitmap.DecodeToWidth(stream, w) : new(stream));
            if (disposeOfImageStream)
                await stream.DisposeAsync();
            return bitmapImage;
        }
    }

    public static UgoiraDownloadFormatToken GetAvailableUgoiraDownloadFormatToken(string? ugoiraDownloadFormat = null)
    {
        if (DownloadFormatService is { } service)
            return service.GetAvailableUgoiraDownloadFormatToken(ugoiraDownloadFormat);

        ugoiraDownloadFormat ??= App.Services?.GetService<AppManagement.Settings.AppSettings>()?.DownloadSettings?.DownloadFormats?.UgoiraDownloadFormat ?? string.Empty;
        var token = new UgoiraDownloadFormatToken(ugoiraDownloadFormat);
        if (token.BuiltInFormat is not null)
        {
            if (token.BuiltInFormat == UgoiraDownloadFormat.Mp4 && !MediaEngine.IsMp4Available)
                return UgoiraDownloadFormatToken.Default;
            return token;
        }

        if (token.ExtensionFormatExtension is { } extension
            && App.Services?.GetService<ExtensionService>()?.GetAnimatedImageFormatProvider(extension) is not null)
            return token;

        return UgoiraDownloadFormatToken.Default;
    }

    public static IllustrationDownloadFormatToken GetAvailableIllustrationDownloadFormatToken(string? illustrationDownloadFormat = null)
    {
        if (DownloadFormatService is { } service)
            return service.GetAvailableIllustrationDownloadFormatToken(illustrationDownloadFormat);

        illustrationDownloadFormat ??= App.Services?.GetService<AppManagement.Settings.AppSettings>()?.DownloadSettings?.DownloadFormats?.IllustrationDownloadFormat ?? string.Empty;
        var token = new IllustrationDownloadFormatToken(illustrationDownloadFormat);
        if (token.BuiltInFormat is IllustrationDownloadFormat.Original)
            return token;

        if (token.ExtensionFormatExtension is { } extension
            && App.Services?.GetService<ExtensionService>()?.GetStaticImageFormatProvider(extension) is not null)
            return token;

        return IllustrationDownloadFormatToken.Default;
    }

    public static NovelDownloadFormatToken GetAvailableNovelDownloadFormatToken(string? novelDownloadFormat = null)
    {
        if (DownloadFormatService is { } service)
            return service.GetAvailableNovelDownloadFormatToken(novelDownloadFormat);

        novelDownloadFormat ??= App.Services?.GetService<AppManagement.Settings.AppSettings>()?.DownloadSettings?.DownloadFormats?.NovelDownloadFormat ?? string.Empty;
        var token = new NovelDownloadFormatToken(novelDownloadFormat);
        if (token.BuiltInFormat is not null)
            return token;

        if (token.ExtensionFormatExtension is { } extension
            && App.Services?.GetService<ExtensionService>()?.GetNovelFormatProvider(extension) is not null)
            return token;

        return NovelDownloadFormatToken.Default;
    }

    /// <summary>
    /// 返回null表示<see cref="UgoiraDownloadFormat.Original"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string? GetUgoiraExtension(string? ugoiraDownloadFormat = null)
    {
        if (DownloadFormatService is { } service)
            return service.GetUgoiraExtension(ugoiraDownloadFormat);

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

    /// <summary>
    /// 返回null表示<see cref="IllustrationDownloadFormat.Original"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string? GetIllustrationExtension(string? illustrationDownloadFormat = null)
    {
        if (DownloadFormatService is { } service)
            return service.GetIllustrationExtension(illustrationDownloadFormat);

        var token = GetAvailableIllustrationDownloadFormatToken(illustrationDownloadFormat);
        if (token.ExtensionFormatExtension is { } extension)
            return extension;

        return (token.BuiltInFormat ?? IllustrationDownloadFormatToken.DefaultBuiltInFormat) switch
        {
            IllustrationDownloadFormat.Original => null,
            _ => throw new ArgumentOutOfRangeException(nameof(illustrationDownloadFormat))
        };
    }

    public static string GetNovelExtension(string? novelDownloadFormat = null) =>
        DownloadFormatService?.GetNovelExtension(novelDownloadFormat) ?? GetNovelExtension(GetAvailableNovelDownloadFormatToken(novelDownloadFormat));

    public static string GetNovelExtension(NovelDownloadFormatToken token)
    {
        if (DownloadFormatService is { } service)
            return service.GetNovelExtension(token);

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

    public static NovelDownloadFormat GetNovelFormat(string extension)
    {
        if (TryGetNovelFormat(extension, out var format))
            return format;

        throw new ArgumentOutOfRangeException(nameof(extension));
    }

    public static bool TryGetNovelFormat(string extension, out NovelDownloadFormat format)
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

    public static string ChangeExtension(string path, string extension)
    {
        return ReplaceFileExtensionTokens(path, extension);
    }

    public static string ReplaceTokenExtensionFromUrl(string path, Uri uri, int setIndex)
    {
        var url = uri.OriginalString;
        return ReplaceTokenExtensionFromUrl(path, url, setIndex);
    }

    public static string ReplaceTokenExtensionFromUrl(string path, string url, int setIndex)
    {
        return ReplaceTokenSetIndex(ReplaceFileExtensionTokens(path, Path.GetExtension(url)), setIndex);
    }

    public static string ReplaceTokenExtensionWithTempExtension(string path, int setIndex)
    {
        return ReplaceTokenSetIndex(ReplaceFileExtensionTokens(path, PixevalTempExtension), setIndex);
    }

    public static string ReplaceTokenSetIndex(string path, int setIndex)
    {
        return ReplaceTokenValues(
                path,
                "<pic_set_index",
                formatter => FormatInteger(setIndex, formatter));
    }

    private static string ReplaceFileExtensionTokens(string path, string extension)
    {
        var extensionValue = extension.StartsWith('.') ? extension[1..] : extension;
        return ReplaceTokenValues(
            path,
            FileExtensionTokenPrefix,
            formatter => FormatString(extensionValue, formatter));
    }

    private static string FormatString(string value, string? formatter) =>
        formatter switch
        {
            "u" => value.ToUpperInvariant(),
            "l" => value.ToLowerInvariant(),
            _ => value
        };

    private static string FormatInteger(long value, string? formatter) =>
        formatter is null
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : value.ToString(formatter, System.Globalization.CultureInfo.InvariantCulture);

    private static string ReplaceTokenValues(string path, string tokenPrefix, Func<string?, string> valueFactory)
    {
        var start = path.IndexOf(tokenPrefix, StringComparison.Ordinal);
        if (start < 0)
            return path;

        var builder = new StringBuilder(path.Length);
        var position = 0;
        while (start >= 0)
        {
            _ = builder.Append(path, position, start - position);

            var afterName = start + tokenPrefix.Length;
            if (afterName >= path.Length)
            {
                _ = builder.Append(path, start, path.Length - start);
                break;
            }

            switch (path[afterName])
            {
                case '>':
                    _ = builder.Append(valueFactory(null));
                    position = afterName + 1;
                    break;
                case ':':
                    var formatterStart = afterName + 1;
                    var tokenEnd = path.IndexOf('>', formatterStart);
                    if (tokenEnd < 0)
                    {
                        _ = builder.Append(path, start, path.Length - start);
                        position = path.Length;
                        break;
                    }

                    _ = builder.Append(valueFactory(path[formatterStart..tokenEnd]));
                    position = tokenEnd + 1;
                    break;
                default:
                    _ = builder.Append(path, start, tokenPrefix.Length);
                    position = afterName;
                    break;
            }

            start = path.IndexOf(tokenPrefix, position, StringComparison.Ordinal);
        }

        if (position < path.Length)
            _ = builder.Append(path, position, path.Length - position);

        return builder.ToString();
    }

    public static string RemoveTokenExtension(string path)
    {
        var withoutSeparatedTokens = ReplaceTokenValues(path, "." + FileExtensionTokenPrefix, static _ => "");
        return ReplaceTokenValues(withoutSeparatedTokens, FileExtensionTokenPrefix, static _ => "");
    }

    extension(object? artworkInfo)
    {
        public int TryGetSetIndex()
        {
            return artworkInfo is Native.Mako.Illustration illust ? illust.SetIndex : -1;
        }
    }
}
