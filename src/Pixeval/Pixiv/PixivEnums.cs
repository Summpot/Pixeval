// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixeval.Pixiv;

public enum AiType
{
    NotSpecified = 0,
    NotAiGenerated = 1,
    AiGenerated = 2
}

public enum TargetFilter
{
    [JsonStringEnumMemberName("for_android")]
    ForAndroid,

    [JsonStringEnumMemberName("for_ios")]
    ForIos
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
