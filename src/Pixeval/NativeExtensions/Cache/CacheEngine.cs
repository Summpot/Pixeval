// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pixeval.Native.Cache;

public partial class CacheEngine
{
    private sealed class ActionCachePreviewCallback(
        Action<byte[]>? onPreview,
        Action<ulong, ulong>? onProgress) : ICachePreviewCallback
    {
        public void OnPreviewFrame(byte[] frameData) => onPreview?.Invoke(frameData);
        public void OnProgress(ulong downloadedBytes, ulong totalBytes) => onProgress?.Invoke(downloadedBytes, totalBytes);
    }

    public async Task<Stream?> GetOrFetchStreamAsync(
        string url,
        string? referer = null,
        IProgress<double>? progress = null,
        Action<byte[]>? onPreview = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            ActionCachePreviewCallback? callback = null;
            if (progress is not null || onPreview is not null)
            {
                callback = new ActionCachePreviewCallback(
                    onPreview,
                    progress is null ? null : (downloaded, total) =>
                    {
                        if (total > 0)
                        {
                            var pct = (double)downloaded / total * 100.0;
                            progress.Report(Math.Min(100.0, pct));
                        }
                    });
            }

            var bytes = await GetOrFetchAsync(url, referer, callback);
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(100.0);
            return new MemoryStream(bytes, writable: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public bool TryCache(string key, Stream stream)
    {
        try
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return TryCache(key, ms.ToArray());
        }
        catch
        {
            return false;
        }
    }

    public bool TryCache(string key, byte[] data)
    {
        try
        {
            Put(key, data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryReadCache(string key, out Stream? readonlyStream)
    {
        try
        {
            var bytes = Get(key);
            if (bytes is null)
            {
                readonlyStream = null;
                return false;
            }

            readonlyStream = new MemoryStream(bytes, writable: false);
            return true;
        }
        catch
        {
            readonlyStream = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to read and decompress a Planar RGBA cached image directly into an Avalonia WriteableBitmap
    /// with zero intermediate allocations on the managed heap.
    /// </summary>
    public WriteableBitmap? TryReadPlanarBitmap(string key)
    {
        try
        {
            var dims = GetPlanarDimensions(key);
            if (dims is null)
                return null;

            var width = (int)dims.Width;
            var height = (int)dims.Height;
            if (width <= 0 || height <= 0)
                return null;

            var wb = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var fb = wb.Lock())
            {
                try
                {
                    _ = DecompressPlanarToMemory(key, (ulong)(nint)fb.Address, (ulong)(fb.RowBytes * height));
                }
                catch
                {
                    wb.Dispose();
                    return null;
                }
            }

            return wb;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decodes a raw image byte stream in Rust, saves it as Planar LZ4 for future instant zero-copy loading,
    /// and returns the decoded WriteableBitmap.
    /// </summary>
    public WriteableBitmap? CacheAndDecodePlanarBitmap(string key, byte[] rawBytes)
    {
        try
        {
            var planarResult = DecodeAndCachePlanar(key, rawBytes);
            var width = (int)planarResult.Width;
            var height = (int)planarResult.Height;
            if (width <= 0 || height <= 0)
                return null;

            var wb = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var fb = wb.Lock())
            {
                Marshal.Copy(planarResult.BgraData, 0, fb.Address, planarResult.BgraData.Length);
            }

            return wb;
        }
        catch
        {
            return null;
        }
    }
}
