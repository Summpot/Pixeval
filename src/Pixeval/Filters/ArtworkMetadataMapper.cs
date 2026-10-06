// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Linq;
using Misaki;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Filters;

namespace Pixeval.Filters;

public static class ArtworkMetadataMapper
{
    public static ArtworkMetadata ToArtworkMetadata(this IArtworkInfo info)
    {
        var id = info.Id.ToString();
        var title = info.Title ?? string.Empty;

        string authorName = string.Empty;
        string authorAccount = string.Empty;
        if (info is IWorkEntry work)
        {
            authorName = work.User?.Name ?? string.Empty;
            authorAccount = work.User?.Account ?? string.Empty;
        }
        else if (info.Authors.FirstOrDefault() is { } author)
        {
            authorName = author.Name;
        }

        var tags = info.Tags
            .SelectMany(group => group)
            .Select(t => new ArtworkTag(t.Name, t.TranslatedName))
            .ToList();

        var totalBookmarks = (long) info.TotalFavorite;
        var createDateTimestamp = info.CreateDate.ToUnixTimeSeconds();

        int width = 0;
        int height = 0;
        if (info is IImageSize imageSize)
        {
            width = imageSize.Width;
            height = imageSize.Height;
        }

        int xRestrict = 0;
        if (info.SafeRating.IsR18G)
            xRestrict = 2;
        else if (info.SafeRating.IsR18)
            xRestrict = 1;

        int aiType = info.IsAiGenerated ? 1 : 0;

        int illustrationType = 0;
        if (info.ImageType is ImageType.SingleAnimatedImage || (info as Pixeval.Native.Mako.Illustration)?.Type == IllustrationType.Ugoira)
            illustrationType = 2;
        else if ((info as Pixeval.Native.Mako.Illustration)?.Type == IllustrationType.Manga)
            illustrationType = 1;

        return new ArtworkMetadata(
            id,
            title,
            authorName,
            authorAccount,
            tags,
            totalBookmarks,
            createDateTimestamp,
            width,
            height,
            xRestrict,
            aiType,
            illustrationType);
    }
}
