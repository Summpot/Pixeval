// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using AutoSettingsPage;
using FluentIcons.Common;

namespace Pixeval.Config;

public record HomePageSettings
{
    [SettingsEntry(Symbol.Table, nameof(Strings.AppSettingsHomePageRowsEntryHeader), nameof(Strings.AppSettingsHomePageRowsEntryDescription))]
    public int HomePageRows { get; set; } = 7;

    [SettingsEntry(Symbol.Table, nameof(Strings.AppSettingsHomePageColumnsEntryHeader), nameof(Strings.AppSettingsHomePageColumnsEntryDescription))]
    public int HomePageColumns { get; set; } = 1;

    [SettingsEntry(Symbol.WindowHeaderHorizontal, nameof(Strings.AppSettingsHideHomePageToolbarEntryHeader), nameof(Strings.AppSettingsHideHomePageToolbarEntryDescription))]
    public bool HideHomePageToolbar { get; set; }

    [SettingsEntry(Symbol.AppTitle, nameof(Strings.AppSettingsHideHomePageCardTitleEntryHeader), nameof(Strings.AppSettingsHideHomePageCardTitleEntryDescription))]
    public bool HideHomePageCardTitle { get; set; }
}
