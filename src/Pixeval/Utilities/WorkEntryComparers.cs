// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;

namespace Pixeval.Utilities;

public class WorkEntryPublishDateComparer : IComparer<object>
{
    public static readonly WorkEntryPublishDateComparer Instance = new();

    public int Compare(object? x, object? y)
    {
        if (x is null || y is null)
            return 0;

        var result = GetCreateDate(x).CompareTo(GetCreateDate(y));
        // 比较Hash以保证稳定排序
        return result is 0 ? x.GetHashCode().CompareTo(y.GetHashCode()) : result;
    }

    private static DateTimeOffset GetCreateDate(object obj) => obj switch
    {
        Illustration ill => ill.CreateDateOffset,
        Novel n => n.CreateDateOffset,
        BooruPost bp => bp.CreateDateOffset,
        _ => default
    };
}

public class WorkEntryBookmarkComparer : IComparer<object>
{
    public static readonly WorkEntryBookmarkComparer Instance = new();

    public int Compare(object? x, object? y)
    {
        if (x is null || y is null)
            return 0;

        var result = GetTotalBookmarks(x).CompareTo(GetTotalBookmarks(y));
        // 比较Hash以保证稳定排序
        return result is 0 ? x.GetHashCode().CompareTo(y.GetHashCode()) : result;
    }

    private static long GetTotalBookmarks(object obj) => obj switch
    {
        Illustration ill => (long) ill.TotalBookmarks,
        Novel n => (long) n.TotalBookmarks,
        BooruPost bp => (long) bp.Score,
        _ => 0
    };
}
