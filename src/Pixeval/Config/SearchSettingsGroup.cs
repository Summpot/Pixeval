// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Text.Json.Serialization;
using AutoSettingsPage;
using FluentIcons.Common;

namespace Pixeval.Config;

public record SearchSettingsGroup
{
    [SettingsEntry(Symbol.ArrowTrending, nameof(Strings.AppSettingsRankOptionEntryHeader), nameof(Strings.AppSettingsRankOptionEntryDescription))]
    public RankOptionsSettings RankOptions { get; set; } = new();

    [SettingsEntry(
        Symbol.Key,
        nameof(Strings.AppSettingsSauceNaoApiKeyEntryHeader),
        nameof(Strings.AppSettingsSauceNaoApiKeyEntryDescription),
        nameof(Strings.AppSettingsSauceNaoApiKeyEntryPlaceholder),
        DescriptionLink = "https://saucenao.com/user.php?page=search-api")]
    public string SauceNaoApiKey { get; set; } = "";

    [SettingsEntry(Symbol.Grid, nameof(Strings.AppSettingsSimpleWorkTypeEntryHeader), nameof(Strings.AppSettingsSimpleWorkTypeEntryDescription))]
    public SimpleWorkType DefaultSimpleWorkType { get; set; }

    [JsonIgnore]
    public WorkType WorkType => DefaultSimpleWorkType is SimpleWorkType.Illustration
        ? WorkType.Illustration
        : WorkType.Novel;
}
