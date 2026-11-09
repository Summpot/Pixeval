// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Misaki;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;

namespace Pixeval.Utilities.IO.Caching;

public static class CacheHelper
{
    public static string CachePath => Path.Combine(AppInfo.CacheFolder, "FileCache");

    private static FileCache _FileCache => App.AppViewModel.FileCache;

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

    public static Task PurgeCacheAsync(CancellationToken token = default) => _FileCache.PurgeAsync(token);

    internal static void InvalidateImage(string key)
    {
        if (App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache)
            _ = _FileCache.TryRemove(key);
    }

    public static Task EnforceCacheSizeLimitAsync(CancellationToken token = default)
    {
        var settings = App.AppViewModel.AppSettings.ApplicationSettings;
        if (!settings.FileCache.LimitFileCacheSize)
            return Task.CompletedTask;

        return _FileCache.EnforceSizeLimitAsync(GetCacheSizeLimitInBytes(), token);
    }

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
                var delaySource = frame.ZipImageDelays
                    ?? throw new InvalidDataException("The animation archive has no frame delays.");
                await delaySource.TryPreloadListAsync(platform, token: token);
                var delays = delaySource.ToArray();
                zip = await Streams.ReadZipAsync(sourceStream, false, token);
                token.ThrowIfCancellationRequested();
                if (zip.Count == 0 || zip.Count != delays.Length)
                    throw new InvalidDataException("The animation archive must contain at least one frame and exactly one delay per frame.");
                var loadedBitmap = IAnimatedBitmap.Load(zip, delays, true);
                zip = null;
                return loadedBitmap;
            }
            finally
            {
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
            if (TryGetStream(key) is { } stream)
                return IAnimatedBitmap.Load(stream, true);
            var useFileCache = App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache;

            var client = App.AppViewModel.GetRequiredPlatformService<IDownloadHttpClientService>(platform)
                .GetImageDownloadClient();
            if (await client.DownloadMemoryStreamAsync(frameUri, progress, token: token, onDataAvailable: onDataAvailable) is
                Result<Stream>.Success(var s))
            {
                if (useFileCache)
                {
                    _ = TryCacheStream(key, s);
                    s.Position = 0;
                }

                return IAnimatedBitmap.Load(s, true);
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
        var frames = frame.MultiImageUris
            ?? throw new InvalidDataException("The animation has no image frames.");
        await frames.TryPreloadListAsync(platform, token: token);
        var count = frames.Count;
        if (count == 0)
            throw new InvalidDataException("The animation has no image frames.");
        var imageList = new List<Stream>(count);
        var delayList = new List<int>(count);
        var ratio = 1d / count;
        var startProgress = 0d;
        IAnimatedBitmap? bitmap = null;
        try
        {
            foreach (var (uri, msDelay) in frames)
            {
                token.ThrowIfCancellationRequested();
                var sp = startProgress;
                var stream = await GetStreamAsync(platform, uri.OriginalString,
                    progress?.Let(t => new Progress<double>(d => t.Report(sp + (ratio * d)))),
                    onDataAvailable, token)
                    ?? throw new InvalidDataException("An animation frame could not be downloaded.");
                // A placeholder frame would make an incomplete original look
                // successful. Keep every acquired stream owned until all frames
                // are available, then let the decoder take ownership together.
                imageList.Add(stream);
                delayList.Add(msDelay);
                startProgress += 100 * ratio;
            }

            token.ThrowIfCancellationRequested();
            bitmap = IAnimatedBitmap.Load(imageList, delayList, true);
            return bitmap;
        }
        finally
        {
            if (bitmap is null)
                foreach (var image in imageList)
                    await image.DisposeAsync();
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
        PixelSize? desiredSize = null,
        CancellationToken decodeToken = default,
        CancellationToken token = default)
    {
        try
        {
            return await GetStreamAsync(platform, key, progress, token: token).ConfigureAwait(false) is { } stream
                ? await stream.DecodeBitmapImageAsync(true, desiredWidth, desiredSize, decodeToken).ConfigureAwait(false)
                : WrappedImageNotAvailable.Value;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || decodeToken.IsCancellationRequested)
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
    /// 本方法会根据<see cref="ApplicationSettingsGroup.UseFileCache"/>判断是否使用文件缓存
    /// </summary>
    /// <returns><see langword="null"/>表示下载失败</returns>
    private static async ValueTask<Stream?> GetStreamAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        Func<Stream, CancellationToken, Task>? onDataAvailable = null,
        CancellationToken token = default)
    {
        if (TryGetStream(key) is { } stream)
            return stream;
        var useFileCache = App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache;

        if (await App.AppViewModel.AppServiceProvider.GetRequiredKeyedService<IDownloadHttpClientService>(platform)
                .GetImageDownloadClient()
                .DownloadMemoryStreamAsync(new Uri(key), progress, token: token, onDataAvailable: onDataAvailable) is Result<Stream>.Success(var s))
        {
            if (useFileCache)
            {
                _ = TryCacheStream(key, s);
                s.Position = 0;
            }

            return s;
        }

        token.ThrowIfCancellationRequested();
        return null;
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
            return App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache
                ? _FileCache.TryOpen(key)
                : null;
        }
        catch (Exception e)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(TryGetStream), e);
        }

        return null;
    }

    /// <exception cref="InvalidOperationException"/>
    internal static FileCacheWriteResult TryCacheStream(string key, Stream stream)
    {
        if (!App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache)
            throw new InvalidOperationException(
                $"Check {nameof(App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache)} before {nameof(TryCacheStream)}");

        var sizeLimitBytes = App.AppViewModel.AppSettings.ApplicationSettings.FileCache.LimitFileCacheSize
            ? GetCacheSizeLimitInBytes()
            : (long?) null;

        return _FileCache.TryCache(key, stream, sizeLimitBytes);
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
