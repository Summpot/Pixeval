// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixeval.Native.Mako;

public partial record UgoiraMetadata
{
    public IReadOnlyList<int> Delays => [.. Frames.Select(t => t.Delay)];

    public string MediumUrl => ZipUrls.Medium;

    public string LargeUrl => ZipUrls.Medium.Replace("600x600", "1920x1080");

    public string[] GetUgoiraOriginalUrls(string originalSingleUrl)
    {
        var arr = new string[Frames.Count];
        for (var i = 0; i < Frames.Count; ++i)
            arr[i] = originalSingleUrl.Replace("ugoira0", $"ugoira{i}");
        return arr;
    }

    public (Uri, int)[] GetUgoiraOriginalUrlsAndMsDelays(string originalSingleUrl)
    {
        var arr = new (Uri, int)[Frames.Count];
        for (var i = 0; i < Frames.Count; ++i)
            arr[i] = (new(originalSingleUrl.Replace("ugoira0", $"ugoira{i}")), Frames[i].Delay);
        return arr;
    }
}
