// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia.Media.Imaging;
using Pixeval.Native.Cache;
using Pixeval.Native.Mako;

namespace Pixeval.Services;

public interface IImageProviderService
{
    CacheEngine CacheEngine { get; }

    Bitmap ImageNotAvailable { get; }

    IAnimatedBitmap AnimatedImageNotAvailable { get; }

    ValueTask<Bitmap> GetBitmapAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        int? desiredWidth = null,
        CancellationToken token = default);

    Task<IAnimatedBitmap> GetAnimatedBitmapAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        CancellationToken token = default);

    ValueTask<IAnimatedBitmap> GetSingleImageAsync(
        string platform,
        string url,
        IProgress<double>? progress = null,
        Action<DecodedPreviewFrame>? onPreview = null,
        CancellationToken token = default);

    ValueTask<IAnimatedBitmap> GetSingleImageAsync(
        string platform,
        Uri frameUri,
        IProgress<double>? progress = null,
        Action<DecodedPreviewFrame>? onPreview = null,
        CancellationToken token = default);

    IAnimatedBitmap LoadAnimatedBitmap(Stream stream);

    ValueTask<IAnimatedBitmap> GetUgoiraAnimatedImageAsync(
        string platform,
        Uri zipUri,
        IReadOnlyList<int> delays,
        IProgress<double>? progress = null,
        Action<DecodedPreviewFrame>? onPreview = null,
        CancellationToken token = default);

    ValueTask<Stream?> GetImageStreamAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        CancellationToken token = default);

    Stream? TryGetStream(string key);

    bool TryCacheStream(string key, Stream stream);

    Task PurgeCacheAsync(CancellationToken token = default);

    Task EnforceCacheSizeLimitAsync(CancellationToken token = default);

    void UpdateNetworkOptions(MakoConfigurationDto config);
}
