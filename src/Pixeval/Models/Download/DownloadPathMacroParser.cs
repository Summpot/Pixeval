// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
using Misaki;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Utilities.IO;

namespace Pixeval.Models.Download;

public static partial class DownloadPathMacroParser
{
    private static readonly MetaPathEngine Engine = new();

    public static IReadOnlyList<IMacro> MacroProvider => MacroDefinitions.All;

    public static MacroAnalysisResult Analyze(string text) => Engine.Analyze(text);

    public static string Reduce(string raw, ParserContext context) =>
        Engine.Reduce(raw, context.ToMacroContext());

    public static MacroContext ToMacroContext(this ParserContext parserContext)
    {
        var artwork = parserContext.ArtworkInfo;
        var subscription = parserContext.WorkSubscription;
        var imageType = artwork.ImageType switch
        {
            ImageType.ImageSet => "ImageSet",
            ImageType.SingleAnimatedImage => "SingleAnimatedImage",
            ImageType.Other => "Other",
            _ => "SingleImage"
        };

        var authorIds = artwork.Authors.Select(a => a.Id).ToList();
        var authorNames = artwork.Authors.Select(a => a.Name).ToList();

        var setIndex = artwork.TryGetSetIndex();

        var isAi = artwork.IsAiGenerated;
        var isR18 = artwork.SafeRating.IsR18 || artwork.SafeRating.IsR18G;
        var isR18G = artwork.SafeRating.IsR18G;
        var isNovel = artwork is Novel or INovelEntry;
        var series = (artwork as IWorkEntry)?.Series;
        var hasSeries = series is not null;
        var seriesId = series?.Id.ToString();
        var seriesTitle = series?.Title;

        return new MacroContext(
            artwork.Id,
            artwork.Title,
            authorIds,
            authorNames,
            artwork.CreateDate.ToString("o"),
            imageType,
            setIndex,
            isAi,
            isR18,
            isR18G,
            isNovel,
            hasSeries,
            seriesId,
            seriesTitle,
            subscription?.Id,
            subscription?.Type.ToString());
    }
}
