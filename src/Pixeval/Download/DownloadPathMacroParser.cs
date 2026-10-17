// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Pixeval.Pixiv;
using Pixeval.Booru;
using Pixeval.Mako;
using Pixeval.SauceNao;
using Pixeval.Common.IO;

namespace Pixeval.Download;

public static partial class DownloadPathMacroParser
{
    public static IReadOnlyList<IMacro> MacroProvider => MacroDefinitions.All;

    public static MacroAnalysisResult Analyze(string text) => PixevalDownloadMethods.AnalyzeMetaPath(text);

    public static string Reduce(string raw, ParserContext context) =>
        PixevalDownloadMethods.ReduceMetaPath(raw, context.ToMacroContext());

    public static MacroContext ToMacroContext(this ParserContext parserContext)
    {
        var artwork = parserContext.ArtworkInfo;
        var subscription = parserContext.WorkSubscription;

        var id = artwork switch
        {
            Illustration i => i.Id.ToString(),
            Mako.Novel n => n.Id.ToString(),
            BooruPost b => b.Id,
            SauceNaoItem s => s.RawId,
            _ => ""
        };

        var title = artwork switch
        {
            Illustration i => i.Title,
            Mako.Novel n => n.Title,
            BooruPost b => b.Title,
            SauceNaoItem s => s.TitleText,
            _ => ""
        };

        var (authorIds, authorNames) = artwork switch
        {
            Illustration i => (new List<string> { i.User.Id.ToString() }, new List<string> { i.User.Name }),
            Mako.Novel n => (new List<string> { n.User.Id.ToString() }, new List<string> { n.User.Name }),
            BooruPost b when !string.IsNullOrWhiteSpace(b.UploaderName) => (new List<string> { b.UploaderName }, new List<string> { b.UploaderName }),
            SauceNaoItem s when !string.IsNullOrWhiteSpace(s.AuthorName) => (new List<string> { s.AuthorName }, new List<string> { s.AuthorName }),
            _ => (new List<string>(), new List<string>())
        };

        var createDate = artwork switch
        {
            Illustration i => i.CreateDateOffset.ToString("o"),
            Mako.Novel n => n.CreateDateOffset.ToString("o"),
            BooruPost b => b.CreateDateOffset.ToString("o"),
            _ => ""
        };

        var imageType = artwork switch
        {
            Illustration { IsPicSet: true } => "ImageSet",
            Illustration { IsPicGif: true } => "SingleAnimatedImage",
            Mako.Novel => "Other",
            _ => "SingleImage"
        };

        var setIndex = artwork.TryGetSetIndex();

        var isAi = artwork switch
        {
            Illustration i => i.IsAiGenerated,
            Mako.Novel n => n.IsAiGenerated,
            _ => false
        };

        var safeRating = artwork switch
        {
            Illustration i => i.SafeRating,
            Mako.Novel n => n.SafeRating,
            BooruPost b => b.SafeRating,
            SauceNaoItem s => s.SafeRating,
            _ => SafeRating.NotSpecified
        };

        var isR18 = safeRating.IsR18 || safeRating.IsR18G;
        var isR18G = safeRating.IsR18G;
        var isNovel = artwork is Mako.Novel;
        var series = (artwork as IWorkEntry)?.Series;
        var hasSeries = series is not null;
        var seriesId = series?.Id.ToString();
        var seriesTitle = series?.Title;

        return new MacroContext(
            id,
            title,
            authorIds,
            authorNames,
            createDate,
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
