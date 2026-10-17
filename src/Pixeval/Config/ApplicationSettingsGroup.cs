// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using AutoSettingsPage;
using FluentIcons.Common;
using Pixeval.Config;

namespace Pixeval.Config;

public record ApplicationSettingsGroup
{
    public DateTime LastCheckedUpdate { get; set; } = DateTime.MinValue;

    public double WindowWidth { get; set; } = 800;

    public double WindowHeight { get; set; } = 600;

    public bool IsMaximized { get; set; }

    // TODO: Not Used
    [SettingsEntry(Symbol.Communication, nameof(Strings.AppSettingsDownloadUpdateAutomaticallyEntryHeader), nameof(Strings.AppSettingsDownloadUpdateAutomaticallyEntryDescription))]
    public bool DownloadUpdateAutomatically { get; set; }

    /// <summary>
    /// "" 表示使用系统默认语言
    /// </summary>
    [SettingsEntry(Symbol.LocalLanguage, nameof(Strings.AppSettingsAppLanguageEntryHeader), nameof(Strings.AppSettingsAppLanguageEntryDescription), nameof(Strings.AppSettingsAppLanguageEntryPlaceholder), "ms-settings:regionlanguage")]
    public string CultureName { get; set; } = "";

    /// <summary>
    /// The Application Theme
    /// </summary>
    [SettingsEntry(Symbol.DarkTheme, nameof(Strings.AppSettingsThemeEntryHeader), nameof(Strings.AppSettingsThemeEntryDescription))]
    public ApplicationTheme Theme { get; set; }

    [SettingsEntry(Symbol.DatabaseLightning, nameof(Strings.AppSettingsLimitFileCacheSizeEntryHeader), nameof(Strings.AppSettingsLimitFileCacheSizeEntryDescription))]
    public FileCacheSettings FileCache { get; set; } = new();

    [SettingsEntry(Symbol.TextFont, nameof(Strings.AppSettingsAppFontFamilyEntryHeader), nameof(Strings.AppSettingsAppFontFamilyEntryDescription), nameof(Strings.AppSettingsAppFontFamilyEntryPlaceholder))]
    public ObservableCollection<string> AppFontFamily { get; set; } = [];

    [SettingsEntry(Symbol.Home, nameof(Strings.AppSettingsHomePageEntryHeader), nameof(Strings.AppSettingsHomePageEntryDescription))]
    public HomePageSettings HomePage { get; set; } = new();
}
