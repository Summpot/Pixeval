// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pixeval.Media;

public static class MediaCodec
{
    public static bool IsMp4Available => PixevalMediaMethods.IsMp4Supported();

    public static byte[] TranscodeBytes(
        byte[] inputBytes,
        ImageCodecFormat targetFormat,
        TranscodeOptions? options = null) =>
        PixevalMediaMethods.TranscodeBytes(inputBytes, targetFormat, options);

    public static Task SynthesizeUgoiraAsync(
        string inputPath,
        string outputPath,
        UgoiraFormat format,
        IReadOnlyList<uint> delaysMs)
    {
        return Task.Run(() => PixevalMediaMethods.SynthesizeUgoira(inputPath, outputPath, format, [.. delaysMs]));
    }

    public static Task PackMangaAsync(
        IReadOnlyList<string> filePaths,
        string outputPath,
        MangaArchiveFormat format)
    {
        return Task.Run(() => PixevalMediaMethods.PackManga([.. filePaths], outputPath, format));
    }

    public static Task TranscodeFileAsync(
        string inputPath,
        string outputPath,
        ImageCodecFormat targetFormat,
        TranscodeOptions? options = null)
    {
        return Task.Run(() => PixevalMediaMethods.TranscodeFile(inputPath, outputPath, targetFormat, options));
    }
}
