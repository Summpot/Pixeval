// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Config;

[System.Text.Json.Serialization.JsonConverter(typeof(RankOptionsSettingsJsonConverter))]
public record RankOptionsSettings
{
    public RankMode IllustrationRankOption { get; set; } = RankMode.Day;

    public RankMode NovelRankOption { get; set; } = RankMode.Day;
}
