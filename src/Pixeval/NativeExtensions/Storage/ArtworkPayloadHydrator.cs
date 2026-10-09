// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Misaki;
using Pixeval.Native.Booru;
using Pixeval.Native.SauceNao;
using MakoIllustration = Pixeval.Native.Mako.Illustration;
using MakoNovel = Pixeval.Native.Mako.Novel;

namespace Pixeval.Native.Storage;

public static class ArtworkPayloadHydrator
{
    public static IArtworkInfo? Hydrate(string? serializeKey, string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            if (!string.IsNullOrWhiteSpace(serializeKey))
            {
                if (serializeKey.Equals(MakoIllustration.LegacyIllustrationToken, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.Equals(typeof(MakoIllustration).FullName, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.Equals("Pixeval.Models.Pixiv.PixivIllustration", StringComparison.OrdinalIgnoreCase)
                    || serializeKey.StartsWith("Illustration", StringComparison.OrdinalIgnoreCase))
                {
                    return MakoIllustration.Deserialize(payloadJson);
                }

                if (serializeKey.Equals(MakoNovel.LegacyNovelToken, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.Equals(typeof(MakoNovel).FullName, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.Equals("Pixeval.Models.Pixiv.PixivNovel", StringComparison.OrdinalIgnoreCase)
                    || serializeKey.StartsWith("Novel", StringComparison.OrdinalIgnoreCase))
                {
                    return MakoNovel.Deserialize(payloadJson);
                }

                if (serializeKey.Equals(BooruPost.LegacyPostToken, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.Equals(BooruPost.NativePostToken, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.Equals(typeof(BooruPost).FullName, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.StartsWith("BooruPost", StringComparison.OrdinalIgnoreCase)
                    || serializeKey.StartsWith("Post", StringComparison.OrdinalIgnoreCase))
                {
                    return BooruPost.Deserialize(payloadJson);
                }

                if (serializeKey.Equals(SauceNaoItem.SauceNaoItemToken, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.Equals(typeof(SauceNaoItem).FullName, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.StartsWith("SauceNao", StringComparison.OrdinalIgnoreCase))
                {
                    return SauceNaoItem.Deserialize(payloadJson);
                }
            }

            // Fallback: try parsing as Illustration first, then Novel, then BooruPost, then SauceNaoItem
            try
            {
                var illust = MakoIllustration.Deserialize(payloadJson);
                if (illust.Id != 0 || illust.ImageUrls != null)
                    return illust;
            }
            catch
            {
                // try next
            }

            try
            {
                var novel = MakoNovel.Deserialize(payloadJson);
                if (novel.Id != 0)
                    return novel;
            }
            catch
            {
                // try next
            }

            try
            {
                return BooruPost.Deserialize(payloadJson);
            }
            catch
            {
                // try next
            }

            try
            {
                return SauceNaoItem.Deserialize(payloadJson);
            }
            catch
            {
                return null;
            }
        }
        catch
        {
            return null;
        }
    }
}
