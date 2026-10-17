// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Text.Json.Serialization;
using AutoSettingsPage;
using FluentIcons.Common;

namespace Pixeval.Config;

[System.Text.Json.Serialization.JsonConverter(typeof(SearchSettingsGroupJsonConverter))]
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
    public SimpleWorkKind DefaultSimpleWorkType { get; set; } = SimpleWorkKind.Illustration;

    [JsonIgnore]
    public WorkKind WorkKind => DefaultSimpleWorkType is SimpleWorkKind.Illustration
        ? WorkKind.Illustration
        : WorkKind.Novel;
}
