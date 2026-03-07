// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Pixeval.Utilities.IO.Caching;

/// <summary>
/// Abstraction for thumbnail bitmap cache access.
/// Implementations can be swapped with minimal changes (e.g. ZoneTree-backed cache).
/// </summary>
public interface IThumbnailBitmapProvider
{
    ValueTask<Bitmap?> GetBitmapAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Global entry point for thumbnail bitmap cache provider.
/// </summary>
public static class ThumbnailBitmapProvider
{
    public static IThumbnailBitmapProvider Current { get; set; } = new CacheHelperThumbnailBitmapProvider();

    private sealed class CacheHelperThumbnailBitmapProvider : IThumbnailBitmapProvider
    {
        public async ValueTask<Bitmap?> GetBitmapAsync(string key, CancellationToken cancellationToken = default)
            => await CacheHelper.TryGetBitmapAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
