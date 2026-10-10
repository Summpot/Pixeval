// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.Models.Extensions;

namespace Pixeval.Native.Download;

public sealed class DownloadFormatEncoderAdapter(ExtensionService extensions) : IDownloadFormatEncoder
{
    public string EncodeStaticImage(string sourcePath, string destinationPath, string extension)
    {
        var provider = extensions.GetStaticImageFormatProvider(extension);
        if (provider is null)
            return extension;

        return Encode(() =>
        {
            EnsureParent(destinationPath);
            using var stream = File.OpenRead(sourcePath);
            provider.FormatImageAsync(stream, destinationPath).GetAwaiter().GetResult();
        });
    }

    public string EncodeAnimated(List<string> framePaths, List<uint> delaysMs, string destinationPath, string extension)
    {
        var provider = extensions.GetAnimatedImageFormatProvider(extension);
        if (provider is null)
            return extension;

        if (framePaths.Count != delaysMs.Count)
            return $"Frame count {framePaths.Count} does not match delay count {delaysMs.Count}.";

        return Encode(() =>
        {
            EnsureParent(destinationPath);
            var streams = new List<Stream>(framePaths.Count);
            try
            {
                var frames = new Dictionary<Stream, int>(framePaths.Count);
                for (var index = 0; index < framePaths.Count; index++)
                {
                    var stream = File.OpenRead(framePaths[index]);
                    streams.Add(stream);
                    frames.Add(stream, (int)delaysMs[index]);
                }

                provider.FormatImageAsync(frames, destinationPath).GetAwaiter().GetResult();
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        });
    }

    public string EncodeNovel(string text, List<string> imagePaths, string destinationPath, string extension)
    {
        var provider = extensions.GetNovelFormatProvider(extension);
        if (provider is null)
            return extension;

        return Encode(() =>
        {
            EnsureParent(destinationPath);
            var streams = new List<Stream>(imagePaths.Count);
            try
            {
                var images = new Dictionary<string, Stream>(imagePaths.Count);
                foreach (var path in imagePaths)
                {
                    var stream = File.OpenRead(path);
                    streams.Add(stream);
                    images[Path.GetFileName(path)] = stream;
                }

                provider.FormatNovelAsync(text, destinationPath, images).GetAwaiter().GetResult();
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        });
    }

    private static string Encode(Action action)
    {
        try
        {
            action();
            return "";
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }

    private static void EnsureParent(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
    }
}
