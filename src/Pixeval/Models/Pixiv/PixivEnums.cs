// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text.Json;
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

[JsonConverter(typeof(RankOptionJsonConverter))]
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

public sealed class RankOptionJsonConverter : JsonConverter<RankOption>
{
    public override RankOption Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (!string.IsNullOrEmpty(str))
            {
                if (Enum.TryParse<RankOption>(str, ignoreCase: true, out var result))
                    return result;

                return str switch
                {
                    "day" => RankOption.Day,
                    "week" => RankOption.Week,
                    "month" => RankOption.Month,
                    "day_male" => RankOption.DayMale,
                    "day_female" => RankOption.DayFemale,
                    "day_manga" => RankOption.DayManga,
                    "week_manga" => RankOption.WeekManga,
                    "month_manga" => RankOption.MonthManga,
                    "week_original" => RankOption.WeekOriginal,
                    "week_rookie" => RankOption.WeekRookie,
                    "day_r18" => RankOption.DayR18,
                    "day_male_r18" => RankOption.DayMaleR18,
                    "day_female_r18" => RankOption.DayFemaleR18,
                    "week_r18" => RankOption.WeekR18,
                    "week_r18g" => RankOption.WeekR18G,
                    "day_ai" => RankOption.DayAi,
                    "day_r18_ai" => RankOption.DayR18Ai,
                    "week_ai" => RankOption.WeekAi,
                    "week_ai_r18" => RankOption.WeekAiR18,
                    _ => default
                };
            }
        }
        else if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var intVal))
        {
            return (RankOption)intVal;
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, RankOption value, JsonSerializerOptions options)
    {
        var str = value switch
        {
            RankOption.Day => "day",
            RankOption.Week => "week",
            RankOption.Month => "month",
            RankOption.DayMale => "day_male",
            RankOption.DayFemale => "day_female",
            RankOption.DayManga => "day_manga",
            RankOption.WeekManga => "week_manga",
            RankOption.MonthManga => "month_manga",
            RankOption.WeekOriginal => "week_original",
            RankOption.WeekRookie => "week_rookie",
            RankOption.DayR18 => "day_r18",
            RankOption.DayMaleR18 => "day_male_r18",
            RankOption.DayFemaleR18 => "day_female_r18",
            RankOption.WeekR18 => "week_r18",
            RankOption.WeekR18G => "week_r18g",
            RankOption.DayAi => "day_ai",
            RankOption.DayR18Ai => "day_r18_ai",
            RankOption.WeekAi => "week_ai",
            RankOption.WeekAiR18 => "week_ai_r18",
            _ => value.ToString()
        };
        writer.WriteStringValue(str);
    }
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
