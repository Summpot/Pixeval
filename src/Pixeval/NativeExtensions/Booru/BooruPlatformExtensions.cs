// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Models;

namespace Pixeval.Native.Booru;

public static class BooruPlatformExtensions
{
    public static string ToPlatformString(this BooruPlatform platform) => platform switch
    {
        BooruPlatform.Danbooru => PlatformConstants.Danbooru,
        BooruPlatform.Gelbooru => PlatformConstants.Gelbooru,
        BooruPlatform.Yandere => PlatformConstants.Yandere,
        BooruPlatform.Sankaku => PlatformConstants.Sankaku,
        BooruPlatform.Rule34 => PlatformConstants.Rule34,
        _ => platform.ToString().ToLowerInvariant()
    };

    public static BooruPlatform FromPlatformString(string platform) => platform.Trim().ToLowerInvariant() switch
    {
        "danbooru" => BooruPlatform.Danbooru,
        "gelbooru" => BooruPlatform.Gelbooru,
        "yandere" => BooruPlatform.Yandere,
        "sankaku" => BooruPlatform.Sankaku,
        "rule34" => BooruPlatform.Rule34,
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported booru platform")
    };
}
