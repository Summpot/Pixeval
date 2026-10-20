// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Pixeval.Common.IO;

public static partial class IoHelper
{
    private const string FileExtensionTokenPrefix = "<ext";

    extension(Stream stream)
    {
        public async Task<Bitmap> DecodeBitmapImageAsync(
            bool disposeOfImageStream,
            int? desiredWidth = null,
            PixelSize? desiredSize = null,
            CancellationToken token = default
        )
        {
            try
            {
                token.ThrowIfCancellationRequested();

                return await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    if (desiredSize is { } size)
                    {
                        var position = stream.Position;
                        using (var codec = SKCodec.Create(new SKManagedStream(stream, false)))
                        {
                            if (codec is null)
                                throw new ArgumentException("Unable to read image dimensions.", nameof(stream));
                            var info = codec.Info;
                            var scale = Math.Min(1, Math.Max((double) size.Width / info.Width, (double) size.Height / info.Height));
                            desiredWidth = Math.Clamp((int) Math.Ceiling(info.Width * scale), 1, info.Width);
                        }
                        stream.Position = position;
                        token.ThrowIfCancellationRequested();
                    }

                    // Synchronous decoding cannot be interrupted. The caller owns any
                    // result that becomes obsolete after this check.
                    var bitmap = desiredWidth is { } w
                        ? Bitmap.DecodeToWidth(stream, w)
                        : new(stream);

                    return bitmap;
                }).ConfigureAwait(false);
            }
            finally
            {
                if (disposeOfImageStream)
                    await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public static string ChangeExtension(string path, string extension)
    {
        return ReplaceFileExtensionTokens(path, extension);
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
            return artworkInfo is Mako.Illustration illust ? illust.SetIndex : -1;
        }
    }
}
