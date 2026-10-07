// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Imouto.BooruParser;
using Misaki;
using Pixeval.Utilities;
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
                    return MakoIllustration.Deserialize(payloadJson).Apply(t => t.IsFavorite = false);
                }

                if (serializeKey.Equals(MakoNovel.LegacyNovelToken, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.Equals(typeof(MakoNovel).FullName, StringComparison.OrdinalIgnoreCase)
                    || serializeKey.Equals("Pixeval.Models.Pixiv.PixivNovel", StringComparison.OrdinalIgnoreCase)
                    || serializeKey.StartsWith("Novel", StringComparison.OrdinalIgnoreCase))
                {
                    return MakoNovel.Deserialize(payloadJson).Apply(t => t.IsFavorite = false);
                }

                if (serializeKey.Equals(typeof(Post).FullName, StringComparison.OrdinalIgnoreCase))
                {
                    return Post.Deserialize(payloadJson);
                }
            }

            // Fallback: try parsing as Illustration first, then Novel, then Post
            try
            {
                var illust = MakoIllustration.Deserialize(payloadJson);
                if (illust.Id != 0 || illust.ImageUrls != null)
                    return illust.Apply(t => t.IsFavorite = false);
            }
            catch
            {
                // try next
            }

            try
            {
                var novel = MakoNovel.Deserialize(payloadJson);
                if (novel.Id != 0)
                    return novel.Apply(t => t.IsFavorite = false);
            }
            catch
            {
                // try next
            }

            try
            {
                return Post.Deserialize(payloadJson);
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
