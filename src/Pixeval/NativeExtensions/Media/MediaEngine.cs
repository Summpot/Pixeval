// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Pixeval.Native.Media;

public partial class MediaEngine
{
    private static readonly Lazy<MediaEngine> LazyInstance = new(() => new MediaEngine());

    public static MediaEngine Shared => LazyInstance.Value;

    public static bool IsMp4Available => Shared.IsMp4Supported();

    public Task SynthesizeUgoiraAsync(
        string inputPath,
        string outputPath,
        UgoiraFormat format,
        IReadOnlyList<uint> delaysMs)
    {
        return Task.Run(() => Shared.SynthesizeUgoira(inputPath, outputPath, format, [.. delaysMs]));
    }

    public Task PackMangaAsync(
        IReadOnlyList<string> filePaths,
        string outputPath,
        MangaArchiveFormat format)
    {
        return Task.Run(() => Shared.PackManga([.. filePaths], outputPath, format));
    }

    public Task TranscodeFileAsync(
        string inputPath,
        string outputPath,
        ImageCodecFormat targetFormat,
        TranscodeOptions? options = null)
    {
        return Task.Run(() => Shared.TranscodeFile(inputPath, outputPath, targetFormat, options));
    }
}
