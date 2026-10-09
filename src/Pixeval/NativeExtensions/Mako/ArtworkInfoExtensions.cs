// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Misaki;
using Pixeval.Collections;
using Pixeval.Models.Options;

namespace Pixeval.Native.Mako;

public static class ArtworkInfoExtensions
{
    public static string GetThumbnailUrl(this IArtworkInfo workEntry, ThumbnailUrlOption option = ThumbnailUrlOption.Medium)
    {
        return option switch
        {
            ThumbnailUrlOption.Large => workEntry.Thumbnails.PickClosestHeight(600)?.ImageUri.OriginalString ?? "",
            ThumbnailUrlOption.Medium => workEntry.Thumbnails.PickClosestHeight(300)?.ImageUri.OriginalString ?? "",
            ThumbnailUrlOption.SquareMedium => workEntry.Thumbnails.PickClosestHeight(180)?.ImageUri.OriginalString ?? "",
            _ => throw new ArgumentOutOfRangeException(nameof(option))
        };
    }

    public static IEnumerable<ISortDescription<IArtworkInfo>> GetSortDescription(LocalSortOption sortOption)
    {
        if (sortOption is LocalSortOption.DoNotSort)
            yield break;
        yield return sortOption switch
        {
            LocalSortOption.PopularityDescending => ISortDescription<IArtworkInfo>.Create(t => t.TotalFavorite, true),
            LocalSortOption.PublishDateDescending => ISortDescription<IArtworkInfo>.Create(t => t.CreateDate, true),
            LocalSortOption.PublishDateAscending => ISortDescription<IArtworkInfo>.Create(t => t.CreateDate),
            LocalSortOption.DoNotSort or _ => throw new ArgumentOutOfRangeException(nameof(sortOption))
        };
        yield return ISortDescription<IArtworkInfo>.Create(t => t.Id);
    }

    public static async ValueTask TryPreloadListAsync<T>(this IPreloadableList<T> list, IPlatformInfo platform, CancellationToken token = default)
    {
        if (!list.IsPreloaded)
            await list.PreloadListAsync(App.AppViewModel.GetRequiredPlatformService<IGetArtworkService>(platform.Platform), token);
    }

    public static async ValueTask TryPreloadListAsync<T>(this IPreloadableList<T> list, string platform, CancellationToken token = default)
    {
        if (!list.IsPreloaded)
            await list.PreloadListAsync(App.AppViewModel.GetRequiredPlatformService<IGetArtworkService>(platform), token);
    }
}
