// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Misaki;

namespace Pixeval.Utilities;

public class WorkEntryPublishDateComparer : IComparer<IArtworkInfo>
{
    public static readonly WorkEntryPublishDateComparer Instance = new();

    public int Compare(IArtworkInfo? x, IArtworkInfo? y)
    {
        if (x is null || y is null)
            return 0;

        var result = x.CreateDate.CompareTo(y.CreateDate);
        // 比较Hash以保证稳定排序
        return result is 0 ? x.GetHashCode().CompareTo(y.GetHashCode()) : result;
    }
}

public class WorkEntryBookmarkComparer : IComparer<IArtworkInfo>
{
    public static readonly WorkEntryBookmarkComparer Instance = new();

    public int Compare(IArtworkInfo? x, IArtworkInfo? y)
    {
        if (x is null || y is null)
            return 0;

        var result = x.TotalFavorite.CompareTo(y.TotalFavorite);
        // 比较Hash以保证稳定排序
        return result is 0 ? x.GetHashCode().CompareTo(y.GetHashCode()) : result;
    }
}
