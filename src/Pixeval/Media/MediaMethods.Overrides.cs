// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#pragma warning disable CS0436

using System.Collections.Generic;
using UniFFISharp;

namespace Pixeval.Media;

public static partial class PixevalMediaMethods
{
    [PreferUniFFI]
    public static partial byte[] TranscodeBytes(byte[] inputBytes, ImageCodecFormat targetFormat, TranscodeOptions? options);

    [PreferUniFFI]
    public static partial void SynthesizeUgoira(string inputPath, string outputPath, UgoiraFormat format, List<uint> frameDelaysMs);

    [PreferUniFFI]
    public static partial void SynthesizeUgoiraFromFrames(List<string> framePaths, string outputPath, UgoiraFormat format, List<uint> frameDelaysMs);

    [PreferUniFFI]
    public static partial void SynthesizeUgoiraFromZip(string zipPath, string outputPath, UgoiraFormat format, List<uint> frameDelaysMs);

    [PreferUniFFI]
    public static partial void PackManga(List<string> filePaths, string outputPath, MangaArchiveFormat format);

    [PreferUniFFI]
    public static partial void TranscodeFile(string inputPath, string outputPath, ImageCodecFormat targetFormat, TranscodeOptions? options);
}
