// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Pixeval.Pixiv;

namespace Pixeval.Mako;

public static class QueryEnums
{
    public static SimpleWorkKind ToSimpleWorkKind(this WorkKind type) =>
        type is WorkKind.Novel ? SimpleWorkKind.Novel : SimpleWorkKind.Illustration;

    public static SafeRating ToSafeRating(this XRestrict restrict, IReadOnlyList<RestrictionFlag>? attributes = null, int sanityLevel = 0) =>
        PixevalMakoMethods.GetSafeRating(restrict, attributes is null ? null : new List<RestrictionFlag>(attributes), sanityLevel) switch
        {
            SafeRatingKind.General => SafeRating.General,
            SafeRatingKind.Sensitive => SafeRating.Sensitive,
            SafeRatingKind.Questionable => SafeRating.Questionable,
            SafeRatingKind.Explicit => SafeRating.Explicit,
            SafeRatingKind.Guro => SafeRating.Guro,
            _ => SafeRating.NotSpecified
        };

    public static IllustContentType? ToIllustContentType(this SearchIllustrationContentType option) => option switch
    {
        SearchIllustrationContentType.Illustration => IllustContentType.Illustration,
        SearchIllustrationContentType.Manga => IllustContentType.Manga,
        SearchIllustrationContentType.Ugoira => IllustContentType.Ugoira,
        _ => null
    };

    public static RatioPattern? ToRatioPattern(this SearchIllustrationRatioPattern option) => option switch
    {
        SearchIllustrationRatioPattern.Landscape => RatioPattern.Landscape,
        SearchIllustrationRatioPattern.Portrait => RatioPattern.Portrait,
        SearchIllustrationRatioPattern.Square => RatioPattern.Square,
        _ => null
    };

    public static NovelContentLengthOption? ToNovelContentLength(this SearchNovelContentLengthOption option) => option switch
    {
        SearchNovelContentLengthOption.TextLength => NovelContentLengthOption.TextLength,
        SearchNovelContentLengthOption.WordCount => NovelContentLengthOption.WordCount,
        SearchNovelContentLengthOption.ReadingTime => NovelContentLengthOption.ReadingTime,
        _ => null
    };

    public static QueryDate ToQueryDate(this DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day);

    public static QueryDate ToQueryDate(this DateTime value) =>
        new(value.Year, value.Month, value.Day);

    public static QueryDate? ToQueryDate(this DateTimeOffset? value) =>
        value is { } v ? v.ToQueryDate() : null;

    public static IllustrationSearchParams IllustrationKeyword(string word) =>
        new(
            Word: word,
            SearchTarget: IllustSearchTarget.PartialMatchForTags,
            Sort: SortOrder.DateDesc,
            SearchAiType: null,
            ContentType: null,
            RatioPattern: null,
            MergePlainKeywordResults: null,
            IncludeTranslatedTagResults: null,
            IncludePotentialViolationWorks: null,
            StartDate: null,
            EndDate: null,
            WidthMin: null,
            WidthMax: null,
            HeightMin: null,
            HeightMax: null,
            Tool: null);

    public static NovelSearchParams NovelKeyword(string word) =>
        new(
            Word: word,
            SearchTarget: NovelSearchTarget.PartialMatchForTags,
            Sort: SortOrder.DateDesc,
            SearchAiType: null,
            Lang: null,
            ContentLengthOption: null,
            ContentLengthMin: null,
            ContentLengthMax: null,
            IsOriginalOnly: null,
            Genre: null,
            IsReplaceableOnly: null,
            MergePlainKeywordResults: null,
            IncludeTranslatedTagResults: null,
            IncludePotentialViolationWorks: null,
            StartDate: null,
            EndDate: null);
}
