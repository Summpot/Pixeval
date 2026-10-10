// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Native.SauceNao;

namespace Pixeval.Native.Mako;

public static class ArtworkInfoExtensions
{
    public static string GetThumbnailUrl(this object? workEntry, ThumbnailUrlOption option = ThumbnailUrlOption.Medium)
    {
        return workEntry switch
        {
            Illustration illust => option switch
            {
                ThumbnailUrlOption.Large => illust.ImageUrls?.Large ?? illust.ImageUrls?.Medium ?? "",
                ThumbnailUrlOption.SquareMedium => illust.ImageUrls?.SquareMedium ?? illust.ImageUrls?.Medium ?? "",
                _ => illust.ImageUrls?.Medium ?? illust.ImageUrls?.SquareMedium ?? ""
            },
            Novel novel => option switch
            {
                ThumbnailUrlOption.Large => novel.ImageUrls?.Large ?? novel.ImageUrls?.Medium ?? "",
                ThumbnailUrlOption.SquareMedium => novel.ImageUrls?.SquareMedium ?? novel.ImageUrls?.Medium ?? "",
                _ => novel.ImageUrls?.Medium ?? novel.ImageUrls?.SquareMedium ?? ""
            },
            WorkEntry we => GetThumbnailUrl(we.AsWorkEntry, option),
            BooruPost booru => booru.PreviewUrl ?? booru.SampleUrl ?? booru.OriginalUrl ?? "",
            SauceNaoItem sauce => sauce.ThumbnailUrl,
            _ => ""
        };
    }

    public static IComparer<object>? GetComparer(LocalSortOption sortOption)
    {
        if (sortOption is LocalSortOption.DoNotSort)
            return null;

        return Comparer<object>.Create((x, y) =>
        {
            var cmp = sortOption switch
            {
                LocalSortOption.PopularityDescending => GetBookmarks(y).CompareTo(GetBookmarks(x)),
                LocalSortOption.PublishDateDescending => GetCreateDate(y).CompareTo(GetCreateDate(x)),
                LocalSortOption.PublishDateAscending => GetCreateDate(x).CompareTo(GetCreateDate(y)),
                _ => 0
            };

            return cmp != 0 ? cmp : GetId(x).CompareTo(GetId(y));
        });

        static long GetBookmarks(object? item) => item switch
        {
            Illustration illust => illust.TotalBookmarks,
            Novel novel => novel.TotalBookmarks,
            BooruPost booru => booru.Score,
            WorkEntry we => (long) we.TotalFavorite,
            _ => 0
        };

        static DateTimeOffset GetCreateDate(object? item) => item switch
        {
            Illustration illust => illust.CreateDateOffset,
            Novel novel => novel.CreateDateOffset,
            BooruPost booru => booru.CreateDateOffset,
            WorkEntry we => we.CreateDateOffset,
            _ => default
        };

        static long GetId(object? item) => item switch
        {
            Illustration illust => illust.Id,
            Novel novel => novel.Id,
            BooruPost booru => long.TryParse(booru.Id, out var id) ? id : 0,
            WorkEntry we => we.Id,
            _ => 0
        };
    }
}
