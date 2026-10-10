// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia.Media.Imaging;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.Native.Cache;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.Utilities.IO;

namespace Pixeval.Services;

public sealed class ImageProviderService : IImageProviderService, IDisposable
{
    private readonly AppSettings _appSettings;
    private readonly FileLogger _logger;
    private readonly Lazy<CacheEngine> _cacheEngineLazy;
    private readonly Lazy<Bitmap> _imageNotAvailableLazy;
    private readonly Lazy<IAnimatedBitmap> _animatedImageNotAvailableLazy;

    public ImageProviderService(AppSettings appSettings, FileLogger logger)
    {
        _appSettings = appSettings;
        _logger = logger;

        _cacheEngineLazy = new Lazy<CacheEngine>(() =>
        {
            var capacity = (ulong)GetCacheSizeLimitInBytes();
            var engine = new CacheEngine(CachePath, capacity);
            try
            {
                var cfg = _appSettings.ToMakoConfiguration();
                engine.UpdateNetworkOptions(
                    cfg.DomainFrontingEnabled,
                    cfg.SplitDelayMs,
                    cfg.HostIps,
                    cfg.ProxyUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(nameof(ImageProviderService), ex);
            }
            return engine;
        });

        _imageNotAvailableLazy = new Lazy<Bitmap>(() => new UndisposableBitmap(AppInfo.GetImageNotAvailableStream()));
        _animatedImageNotAvailableLazy = new Lazy<IAnimatedBitmap>(() => IAnimatedBitmap.Load([_imageNotAvailableLazy.Value], [100]));
    }

    public static string CachePath { get; } = Path.Combine(AppInfo.CacheFolder, "FileCache");

    public CacheEngine CacheEngine => _cacheEngineLazy.Value;

    public Bitmap ImageNotAvailable => _imageNotAvailableLazy.Value;

    public IAnimatedBitmap AnimatedImageNotAvailable => _animatedImageNotAvailableLazy.Value;

    public IAnimatedBitmap LoadAnimatedBitmap(Stream stream) => IAnimatedBitmap.Load(stream, true);

    private long GetCacheSizeLimitInBytes()
    {
        var sizeInMegabytes = Math.Max(1, _appSettings.ApplicationSettings.FileCache.FileCacheSizeLimitInMegabytes);
        return sizeInMegabytes * 1024L * 1024L;
    }

    public void UpdateNetworkOptions(MakoConfigurationDto config)
    {
        try
        {
            CacheEngine.UpdateNetworkOptions(
                config.DomainFrontingEnabled,
                config.SplitDelayMs,
                config.HostIps,
                config.ProxyUrl);
        }
        catch (Exception e)
        {
            _logger.LogError(nameof(UpdateNetworkOptions), e);
        }
    }

    public Task PurgeCacheAsync(CancellationToken token = default) =>
        Task.Run(() => CacheEngine.Clear(), token);

    public Task EnforceCacheSizeLimitAsync(CancellationToken token = default) => Task.CompletedTask;

    public async ValueTask<IAnimatedBitmap> GetUgoiraAnimatedImageAsync(
        string platform,
        Uri zipUri,
        IReadOnlyList<int> delays,
        IProgress<double>? progress = null,
        Action<DecodedPreviewFrame>? onPreview = null,
        CancellationToken token = default)
    {
        var key = zipUri.OriginalString;
        var sourceStream = await GetStreamAsync(platform, key, progress, onPreview, token);
        if (sourceStream is null)
            return AnimatedImageNotAvailable;

        IReadOnlyList<MemoryStream>? zip = null;
        try
        {
            zip = await Streams.ReadZipAsync(sourceStream, true);
            sourceStream = null;
            token.ThrowIfCancellationRequested();
            var loadedBitmap = IAnimatedBitmap.Load(zip, delays, true);
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

    public ValueTask<IAnimatedBitmap> GetSingleImageAsync(
        string platform,
        string url,
        IProgress<double>? progress = null,
        Action<DecodedPreviewFrame>? onPreview = null,
        CancellationToken token = default)
        => GetSingleImageAsync(platform, new Uri(url), progress, onPreview, token);

    public async ValueTask<IAnimatedBitmap> GetSingleImageAsync(
        string platform,
        Uri frameUri,
        IProgress<double>? progress = null,
        Action<DecodedPreviewFrame>? onPreview = null,
        CancellationToken token = default)
    {
        try
        {
            var key = frameUri.OriginalString;
            var stream = await GetStreamAsync(platform, key, progress, onPreview, token);
            if (stream is not null)
            {
                return IAnimatedBitmap.Load(stream, true);
            }

            token.ThrowIfCancellationRequested();
            return AnimatedImageNotAvailable;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(nameof(GetSingleImageAsync), e);
        }

        return AnimatedImageNotAvailable;
    }

    public async ValueTask<Stream?> GetImageStreamAsync(
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
            _logger.LogError(nameof(GetImageStreamAsync), e);
        }

        return null;
    }

    public async Task<IAnimatedBitmap> GetAnimatedBitmapAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        CancellationToken token = default)
    {
        try
        {
            if (await GetImageStreamAsync(platform, key, progress, token) is { } stream)
                return IAnimatedBitmap.Load(stream, true);
            return AnimatedImageNotAvailable;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(nameof(GetAnimatedBitmapAsync), e);
        }

        return AnimatedImageNotAvailable;
    }

    public async ValueTask<Bitmap> GetBitmapAsync(
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
                if (CacheEngine.TryReadPlanarBitmap(key) is { } planarBmp)
                    return planarBmp;
            }

            if (await GetStreamAsync(platform, key, progress, token: token) is not { } stream)
                return ImageNotAvailable;

            if (desiredWidth is null && stream is MemoryStream ms)
            {
                var bytes = ms.ToArray();
                _ = Task.Run(() =>
                {
                    try
                    {
                        _ = CacheEngine.DecodeAndCachePlanar(key, bytes);
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
            _logger.LogError(nameof(GetBitmapAsync), e);
        }

        return ImageNotAvailable;
    }

    public Stream? TryGetStream(string key)
    {
        try
        {
            return CacheEngine.TryReadCache(key, out var stream) ? stream : null;
        }
        catch (Exception e)
        {
            _logger.LogError(nameof(TryGetStream), e);
        }

        return null;
    }

    public bool TryCacheStream(string key, Stream stream)
    {
        return CacheEngine.TryCache(key, stream);
    }

    private async ValueTask<Stream?> GetStreamAsync(
        string platform,
        string key,
        IProgress<double>? progress = null,
        Action<DecodedPreviewFrame>? onPreview = null,
        CancellationToken token = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (TryGetStream(key) is { } cachedStream)
                return cachedStream;

            var stream = await CacheEngine.GetOrFetchStreamAsync(
                key,
                referer: null,
                progress: progress,
                onPreview: onPreview,
                cancellationToken: token);

            return stream;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(nameof(GetStreamAsync), e);
            token.ThrowIfCancellationRequested();
            return null;
        }
    }

    public void Dispose()
    {
        if (_cacheEngineLazy.IsValueCreated)
        {
            _cacheEngineLazy.Value.Dispose();
        }
    }

    private sealed class UndisposableBitmap(Stream stream) : Bitmap(stream)
    {
        public override void Dispose()
        {
        }
    }
}
