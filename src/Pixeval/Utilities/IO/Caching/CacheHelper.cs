// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Misaki;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;

namespace Pixeval.Utilities.IO.Caching;

public static class CacheHelper
{
    public static string CachePath { get; } = Path.Combine(AppInfo.CacheFolder, "FileCache");

    private static readonly Lazy<CacheEngine> _CacheEngine =
        new(() =>
        {
            var capacity = (ulong)GetCacheSizeLimitInBytes();
            var engine = new CacheEngine(CachePath, capacity);
            try
            {
                if (App.AppViewModel?.AppSettings is { } settings)
                {
                    var cfg = settings.ToMakoConfiguration();
                    engine.UpdateNetworkOptions(
                        cfg.DomainFrontingEnabled,
                        cfg.SplitDelayMs,
                        cfg.HostIps,
                        cfg.ProxyUrl);
                }
            }
            catch
            {
                // ignore
            }
            return engine;
        });

    public static CacheEngine CacheEngine => _CacheEngine.Value;

    public static void UpdateNetworkOptions(MakoConfigurationDto config)
    {
        try
        {
            _CacheEngine.Value.UpdateNetworkOptions(
                config.DomainFrontingEnabled,
                config.SplitDelayMs,
                config.HostIps,
                config.ProxyUrl);
        }
        catch (Exception e)
        {
            App.AppViewModel?.AppServiceProvider?.GetService<FileLogger>()?
                .LogError(nameof(UpdateNetworkOptions), e);
        }
    }

    /// <summary>
    /// Dispose无效果，可以反复用
    /// </summary>
    public static readonly Lazy<Bitmap> WrappedImageNotAvailable =
        new(() => new UndisposableBitmap(AppInfo.GetImageNotAvailableStream()));

    /// <summary>
    /// Dispose无效果，可以反复用
    /// </summary>
    public static readonly Lazy<IAnimatedBitmap> AnimatedImageNotAvailable =
        new(() => IAnimatedBitmap.Load([WrappedImageNotAvailable.Value], [100]));

    public static Task PurgeCacheAsync(CancellationToken token = default) =>
        Task.Run(() => _CacheEngine.Value.Clear(), token);

    public static Task EnforceCacheSizeLimitAsync(CancellationToken token = default) => Task.CompletedTask;

    private static long GetCacheSizeLimitInBytes()
    {
        var sizeInMegabytes =
            Math.Max(1, App.AppViewModel.AppSettings.ApplicationSettings.FileCache.FileCacheSizeLimitInMegabytes);
        return sizeInMegabytes * 1024L * 1024L;
    }

    /// <summary>
    /// 保证<see cref="Stream.Position"/>为0
    /// </summary>
    public static async ValueTask<IAnimatedBitmap> GetSingleAnimatedImageAsync(
        string platform,
        IAnimatedImageFrame frame,
        IProgress<double>? progress = null,
        Func<Stream, CancellationToken, Task>? onDataAvailable = null,
        CancellationToken token = default)
    {
        var key = frame.SingleImageUri;
        ArgumentNullException.ThrowIfNull(key);
        if (frame.PreferredAnimatedImageType is not SingleAnimatedImageType.SingleZipFile
            and not SingleAnimatedImageType.SingleFile)
            throw new InvalidOperationException(
                $"{nameof(IAnimatedImageFrame.PreferredAnimatedImageType)} should be {nameof(SingleAnimatedImageType.SingleZipFile)} or {nameof(SingleAnimatedImageType.SingleFile)}");

        if (frame.PreferredAnimatedImageType is SingleAnimatedImageType.SingleZipFile)
        {
            var sourceStream = await GetStreamAsync(platform, key.OriginalString, progress, onDataAvailable, token);
            if (sourceStream is null)
                return AnimatedImageNotAvailable.Value;

            IReadOnlyList<MemoryStream>? zip = null;
            try
            {
                ArgumentNullException.ThrowIfNull(frame.ZipImageDelays);
                await frame.ZipImageDelays.TryPreloadListAsync(platform, token: token);
                zip = await Streams.ReadZipAsync(sourceStream, true);
                sourceStream = null;
                token.ThrowIfCancellationRequested();
                var loadedBitmap = IAnimatedBitmap.Load(zip, frame.ZipImageDelays, true);
                zip = null;
                return loadedBitmap;
            }
            finally
            {
                if (sourceStream is not null)
                    await sourceStream.DisposeAsync();
                if (zip is not null)
                    foreach (var stream in zip)
                        await stream.DisposeAsync();
            }
        }

        // SingleAnimatedImageType.SingleFile
        if (await GetSingleImageAsync(platform, key, progress, onDataAvailable, token) is { } bitmap)
            return bitmap;

        return AnimatedImageNotAvailable.Value;
    }

    /// <summary>
    /// 保证<see cref="Stream.Position"/>为0
    /// </summary>
    public static ValueTask<IAnimatedBitmap> GetSingleImageAsync(
        string platform,
        IImageFrame frame,
        IProgress<double>? progress = null,
        Func<Stream, CancellationToken, Task>? onDataAvailable = null,
        CancellationToken token = default)
        => GetSingleImageAsync(platform, frame.ImageUri, progress, onDataAvailable, token);

    private static async ValueTask<IAnimatedBitmap> GetSingleImageAsync(
        string platform,
        Uri frameUri,
        IProgress<double>? progress = null,
        Func<Stream, CancellationToken, Task>? onDataAvailable = null,
        CancellationToken token = default)
    {
        try
        {
            var key = frameUri.OriginalString;
            var stream = await GetStreamAsync(platform, key, progress, onDataAvailable, token);
            if (stream is not null)
            {
                return IAnimatedBitmap.Load(stream, true);
            }

            token.ThrowIfCancellationRequested();
            return AnimatedImageNotAvailable.Value;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(GetSingleImageAsync), e);
        }

        return AnimatedImageNotAvailable.Value;
    }

    public static async Task<IAnimatedBitmap> GetAnimatedImageSeparatedAsync(
        string platform,
        IAnimatedImageFrame frame,
        IProgress<double>? progress = null,
        Func<Stream, CancellationToken, Task>? onDataAvailable = null,
        CancellationToken token = default)
    {
        if (frame.PreferredAnimatedImageType is not SingleAnimatedImageType.MultiFiles)
            throw new InvalidOperationException(
                $"{nameof(IAnimatedImageFrame.PreferredAnimatedImageType)} should be {nameof(SingleAnimatedImageType.MultiFiles)}");
        await frame.MultiImageUris!.TryPreloadListAsync(platform, token: token);
        var client = App.AppViewModel.AppServiceProvider.GetRequiredKeyedService<IDownloadHttpClientService>(platform)
            .GetImageDownloadClient();
        var count = frame.MultiImageUris!.Count;
        var imageList = new List<BitmapOrStream>(count);
        var delayList = new List<int>(count);
        var ratio = 1d / count;
        var startProgress = 0d;
        IAnimatedBitmap? bitmap = null;
        try
        {
            foreach (var (uri, msDelay) in frame.MultiImageUris)
            {
                BitmapOrStream stream;
                try
                {
                    var key = uri.OriginalString;
                    if (TryGetStream(key) is { } s)
                        stream = s;
                    else
                    {
                        var sp = startProgress;
                        var s2 = await GetStreamAsync(
                            platform,
                            key,
                            progress?.Let(t => new Progress<double>(d => t.Report(sp + (ratio * d)))),
                            onDataAvailable,
                            token);
                        if (s2 is not null)
                        {
                            stream = s2;
                        }
                        else
                        {
                            stream = WrappedImageNotAvailable.Value;
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                        .LogError(nameof(GetAnimatedImageSeparatedAsync), e);
                    stream = WrappedImageNotAvailable.Value;
                }

                imageList.Add(stream);
                delayList.Add(msDelay);
                startProgress += 100 * ratio;
            }

            bitmap = IAnimatedBitmap.Load(imageList, delayList, true);
            return bitmap;
        }
        finally
        {
            if (bitmap is null)
                foreach (var image in imageList)
                    image.Dispose();
        }
    }

    /// <summary>
    /// 保证<see cref="Stream.Position"/>为0
    /// </summary>
    public static async ValueTask<Stream?> GetImageStreamAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        CancellationToken token = default)
    {
        try
        {
            return await GetStreamAsync(platform, key, progress, token: token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(GetImageStreamAsync), e);
        }

        return null;
    }

    public static async Task<IAnimatedBitmap> GetAnimatedBitmapAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        CancellationToken token = default)
    {
        try
        {
            if (await GetImageStreamAsync(platform, key, progress, token) is { } stream)
                return IAnimatedBitmap.Load(stream, true);
            return AnimatedImageNotAvailable.Value;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(GetAnimatedBitmapAsync), e);
        }

        return AnimatedImageNotAvailable.Value;
    }

    public static async ValueTask<Bitmap> GetBitmapAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        int? desiredWidth = null,
        CancellationToken token = default)
    {
        try
        {
            if (desiredWidth is null)
            {
                if (_CacheEngine.Value.TryReadPlanarBitmap(key) is { } planarBmp)
                    return planarBmp;
            }

            if (await GetStreamAsync(platform, key, progress, token: token) is not { } stream)
                return WrappedImageNotAvailable.Value;

            if (desiredWidth is null && stream is MemoryStream ms)
            {
                var bytes = ms.ToArray();
                _ = Task.Run(() =>
                {
                    try
                    {
                        _ = _CacheEngine.Value.DecodeAndCachePlanar(key, bytes);
                    }
                    catch
                    {
                        // ignore background planar caching error
                    }
                }, CancellationToken.None);
                ms.Position = 0;
            }

            return await stream.DecodeBitmapImageAsync(true, desiredWidth);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(GetBitmapAsync), e);
        }

        return WrappedImageNotAvailable.Value;
    }

    /// <summary>
    /// 获取图片流，优先从文件缓存读取，未命中则请求网络并自动写入缓存
    /// </summary>
    /// <returns><see langword="null"/>表示下载失败</returns>
    private static async ValueTask<Stream?> GetStreamAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        Func<Stream, CancellationToken, Task>? onDataAvailable = null,
        CancellationToken token = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (TryGetStream(key) is { } cachedStream)
                return cachedStream;

            Action<byte[]>? previewAction = onDataAvailable is null ? null : bytes =>
            {
                using var ms = new MemoryStream(bytes, writable: false);
                _ = onDataAvailable(ms, token);
            };

            var stream = await _CacheEngine.Value.GetOrFetchStreamAsync(
                key,
                referer: null,
                progress: progress,
                onPreview: previewAction,
                cancellationToken: token);

            if (stream is not null)
                return stream;

            token.ThrowIfCancellationRequested();

            if (App.AppViewModel?.AppServiceProvider?.GetKeyedService<IDownloadHttpClientService>(platform) is { } clientService)
            {
                var client = clientService.GetImageDownloadClient();
                var ms = new MemoryStream();
                var error = await client.DownloadStreamAsync(
                    ms,
                    new Uri(key),
                    progress: progress,
                    onDataAvailable: onDataAvailable,
                    token: token);
                if (error is null && ms.Length > 0)
                {
                    ms.Position = 0;
                    TryCacheStream(key, ms);
                    ms.Position = 0;
                    return ms;
                }
                await ms.DisposeAsync();
            }

            return null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(GetStreamAsync), e);
            token.ThrowIfCancellationRequested();
            return null;
        }
    }

    /// <summary>
    /// 保证<see cref="Stream.Position"/>为0
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static Stream? TryGetStream(string key)
    {
        try
        {
            return _CacheEngine.Value.TryReadCache(key, out var stream) ? stream : null;
        }
        catch (Exception e)
        {
            App.AppViewModel?.AppServiceProvider?.GetService<FileLogger>()?
                .LogError(nameof(TryGetStream), e);
        }

        return null;
    }

    /// <exception cref="InvalidOperationException"/>
    internal static bool TryCacheStream(string key, Stream stream)
    {
        return _CacheEngine.Value.TryCache(key, stream);
    }
}

file class UndisposableBitmap(Stream stream) : Bitmap(stream)
{
    /// <inheritdoc />
    public override void Dispose()
    {
    }

    public void DisposeForce()
    {
        base.Dispose();
    }
}
