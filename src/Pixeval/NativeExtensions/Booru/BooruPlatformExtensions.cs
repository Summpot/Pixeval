// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Misaki;

namespace Pixeval.Native.Booru;

public static class BooruPlatformExtensions
{
    public static string ToPlatformString(this BooruPlatform platform) => platform switch
    {
        BooruPlatform.Danbooru => IPlatformInfo.Danbooru,
        BooruPlatform.Gelbooru => IPlatformInfo.Gelbooru,
        BooruPlatform.Yandere => IPlatformInfo.Yandere,
        BooruPlatform.Sankaku => IPlatformInfo.Sankaku,
        BooruPlatform.Rule34 => "rule34",
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
