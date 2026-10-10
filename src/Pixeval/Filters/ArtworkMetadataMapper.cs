// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Booru;
using Pixeval.Native.Filters;
using Pixeval.Native.Mako;
using Pixeval.Native.SauceNao;

namespace Pixeval.Filters;

public static class ArtworkMetadataMapper
{
    public static ArtworkMetadata ToArtworkMetadata(this object info)
    {
        return info switch
        {
            Illustration illust => new ArtworkMetadata(
                illust.Id.ToString(),
                illust.Title ?? string.Empty,
                illust.User?.Name ?? string.Empty,
                illust.User?.Account ?? string.Empty,
                illust.Tags.Select(t => new ArtworkTag(t.Name, t.TranslatedName ?? string.Empty)).ToList(),
                illust.TotalBookmarks,
                illust.CreateDateOffset.ToUnixTimeSeconds(),
                (int) illust.Width,
                (int) illust.Height,
                (int) illust.XRestrict,
                illust.IsAiGenerated ? 1 : 0,
                illust.Type == IllustrationType.Ugoira ? 2 : illust.Type == IllustrationType.Manga ? 1 : 0),

            Novel novel => new ArtworkMetadata(
                novel.Id.ToString(),
                novel.Title ?? string.Empty,
                novel.User?.Name ?? string.Empty,
                novel.User?.Account ?? string.Empty,
                novel.Tags.Select(t => new ArtworkTag(t.Name, t.TranslatedName ?? string.Empty)).ToList(),
                novel.TotalBookmarks,
                novel.CreateDateOffset.ToUnixTimeSeconds(),
                0,
                0,
                (int) novel.XRestrict,
                novel.IsAiGenerated ? 1 : 0,
                0),

            BooruPost booru => new ArtworkMetadata(
                booru.Id,
                booru.Title,
                booru.UploaderName ?? string.Empty,
                string.Empty,
                booru.Tags.Select(t => new ArtworkTag(t.Name, string.Empty)).ToList(),
                booru.Score,
                booru.CreateDateOffset.ToUnixTimeSeconds(),
                (int) booru.Width,
                (int) booru.Height,
                booru.SafeRating.IsR18G ? 2 : booru.SafeRating.IsR18 ? 1 : 0,
                0,
                0),

            SauceNaoItem sauce => new ArtworkMetadata(
                sauce.RawId,
                sauce.TitleText,
                sauce.AuthorName ?? string.Empty,
                string.Empty,
                [],
                0,
                0,
                0,
                0,
                sauce.SafeRating.IsR18G ? 2 : sauce.SafeRating.IsR18 ? 1 : 0,
                0,
                0),

            WorkEntry we => ToArtworkMetadata(we.AsWorkEntry),

            _ => new ArtworkMetadata(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                0,
                0,
                0,
                0,
                0,
                0,
                0)
        };
    }
}
