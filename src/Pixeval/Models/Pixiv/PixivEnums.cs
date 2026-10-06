// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Text.Json.Serialization;

namespace Pixeval.Models.Pixiv;

public enum WorkType
{
    [JsonStringEnumMemberName("illust")]
    Illustration,

    [JsonStringEnumMemberName("manga")]
    Manga,

    [JsonStringEnumMemberName("novel")]
    Novel
}

public enum SimpleWorkType
{
    Illustration,
    Novel
}

public enum PrivacyPolicy
{
    [JsonStringEnumMemberName("public")]
    Public,

    [JsonStringEnumMemberName("private")]
    Private
}

public enum RankOption
{
    [JsonStringEnumMemberName("day")]
    Day,

    [JsonStringEnumMemberName("week")]
    Week,

    [JsonStringEnumMemberName("month")]
    Month,

    [JsonStringEnumMemberName("day_male")]
    DayMale,

    [JsonStringEnumMemberName("day_female")]
    DayFemale,

    [JsonStringEnumMemberName("day_manga")]
    DayManga,

    [JsonStringEnumMemberName("week_manga")]
    WeekManga,

    [JsonStringEnumMemberName("month_manga")]
    MonthManga,

    [JsonStringEnumMemberName("week_original")]
    WeekOriginal,

    [JsonStringEnumMemberName("week_rookie")]
    WeekRookie,

    [JsonStringEnumMemberName("day_r18")]
    DayR18,

    [JsonStringEnumMemberName("day_male_r18")]
    DayMaleR18,

    [JsonStringEnumMemberName("day_female_r18")]
    DayFemaleR18,

    [JsonStringEnumMemberName("week_r18")]
    WeekR18,

    [JsonStringEnumMemberName("week_r18g")]
    WeekR18G,

    [JsonStringEnumMemberName("day_ai")]
    DayAi,

    [JsonStringEnumMemberName("day_r18_ai")]
    DayR18Ai,

    [JsonStringEnumMemberName("week_ai")]
    WeekAi,

    [JsonStringEnumMemberName("week_ai_r18")]
    WeekAiR18
}

public static class RankOptionHelper
{
    public static bool IsIllustrationSupport(RankOption rankOption) =>
        rankOption is not RankOption.WeekAi and not RankOption.WeekAiR18;

    public static bool IsNovelSupport(RankOption rankOption) =>
        rankOption is not RankOption.Month
            and not RankOption.DayManga
            and not RankOption.WeekManga
            and not RankOption.MonthManga
            and not RankOption.WeekOriginal
            and not RankOption.DayAi
            and not RankOption.DayR18Ai;
}

public enum AiType
{
    NotSpecified = 0,
    NotAiGenerated = 1,
    AiGenerated = 2
}

public enum IllustrationType
{
    [JsonStringEnumMemberName("illust")]
    Illustration,
    Manga,
    Ugoira
}

public enum XRestrict
{
    Ordinary = 0,
    R18 = 1,
    R18G = 2
}

public enum WorkSortOption
{
    [JsonStringEnumMemberName("date_desc")]
    PublishDateDescending,

    [JsonStringEnumMemberName("date_asc")]
    PublishDateAscending,

    [JsonStringEnumMemberName("popular_desc")]
    PopularityDescending
}

public enum TargetFilter
{
    [JsonStringEnumMemberName("for_android")]
    ForAndroid,

    [JsonStringEnumMemberName("for_ios")]
    ForIos
}

public enum SearchIllustrationTagMatchOption
{
    [JsonStringEnumMemberName("partial_match_for_tags")]
    PartialMatchForTags,

    [JsonStringEnumMemberName("exact_match_for_tags")]
    ExactMatchForTags,

    [JsonStringEnumMemberName("title_and_caption")]
    TitleAndCaption,

    [JsonStringEnumMemberName("keyword")]
    Keyword
}

public enum SearchIllustrationContentType
{
    [JsonStringEnumMemberName("illust_and_manga_and_ugoira")]
    IllustrationAndMangaAndUgoira,

    [JsonStringEnumMemberName("illust_and_ugoira")]
    IllustrationAndUgoira,

    [JsonStringEnumMemberName("illust")]
    Illustration,

    [JsonStringEnumMemberName("manga")]
    Manga,

    [JsonStringEnumMemberName("ugoira")]
    Ugoira
}

public enum SearchIllustrationRatioPattern
{
    All,

    [JsonStringEnumMemberName("landscape")]
    Landscape,

    [JsonStringEnumMemberName("portrait")]
    Portrait,

    [JsonStringEnumMemberName("square")]
    Square
}

public enum SearchNovelTagMatchOption
{
    [JsonStringEnumMemberName("partial_match_for_tags")]
    PartialMatchForTags,

    [JsonStringEnumMemberName("exact_match_for_tags")]
    ExactMatchForTags,

    [JsonStringEnumMemberName("text")]
    Text,

    [JsonStringEnumMemberName("keyword")]
    Keyword
}

public enum SearchNovelContentLengthOption
{
    None,

    [JsonStringEnumMemberName("text_length")]
    TextLength,

    [JsonStringEnumMemberName("word_count")]
    WordCount,

    [JsonStringEnumMemberName("reading_time")]
    ReadingTime
}
