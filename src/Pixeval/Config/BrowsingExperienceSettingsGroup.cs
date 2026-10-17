// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using AutoSettingsPage;
using Avalonia.Layout;
using FluentIcons.Common;
using Pixeval.Config;

namespace Pixeval.Config;

public record BrowsingExperienceSettingsGroup
{
    [SettingsEntry(Symbol.GlanceHorizontal, nameof(Strings.AppSettingsThumbnailLayoutTypeEntryHeader), nameof(Strings.AppSettingsThumbnailLayoutTypeEntryDescription))]
    public ThumbnailLayoutSettings ThumbnailLayout { get; set; } = new();

    [SettingsEntry(Symbol.CardUiPortraitFlip, nameof(Strings.AppSettingsBrowseModeHeader), nameof(Strings.AppSettingsBrowseModeDescription))]
    public BrowseMode BrowseMode { get; set; } = BrowseMode.Swipe;

    [SettingsEntry(Symbol.ArrowBetweenDown, nameof(Strings.AppSettingsBrowseDirectionHeader), nameof(Strings.AppSettingsBrowseDirectionDescription))]
    public Orientation BrowseDirection { get; set; } = Orientation.Horizontal;

    /// <summary>
    /// The target filter that indicates the type of the client
    /// </summary>
    [SettingsEntry(Symbol.CodeBlock, nameof(Strings.AppSettingsTargetAPIPlatformEntryHeader), nameof(Strings.AppSettingsTargetAPIPlatformEntryDescription))]
    public TargetFilter TargetFilter { get; set; } = TargetFilter.ForAndroid;

    [SettingsEntry(Symbol.TagDismiss, nameof(Strings.AppSettingsBlockedTagsEntryHeader), nameof(Strings.AppSettingsBlockedTagsEntryDescription), nameof(Strings.AppSettingsBlockedTagsEntryPlaceholder))]
    public ObservableCollection<string> BlockedTags { get; set; } = [];

    [SettingsEntry(Symbol.Pin, nameof(Strings.AppSettingsPinnedTagsEntryHeader), nameof(Strings.AppSettingsPinnedTagsEntryDescription), nameof(Strings.AppSettingsPinnedTagsEntryPlaceholder))]
    public ObservableCollection<string> PinnedTags { get; set; } = [];

    [JsonIgnore]
    [SettingsEntry(Symbol.PersonProhibited, nameof(Strings.AppSettingsBlockedUsersEntryHeader), nameof(Strings.AppSettingsBlockedUsersEntryDescription))]
    public byte BlockedUsers => 0;

    [SettingsEntry(Symbol.Info, nameof(Strings.AppSettingsOpenWorkInfoByDefaultEntryHeader), nameof(Strings.AppSettingsOpenWorkInfoByDefaultEntryDescription))]
    public bool OpenWorkInfoByDefault { get; set; }

    [SettingsEntry(Symbol.PersonInfo, nameof(Strings.AppSettingsOpenUserInfoByDefaultEntryHeader), nameof(Strings.AppSettingsOpenUserInfoByDefaultEntryDescription))]
    public bool OpenUserInfoByDefault { get; set; } = true;

    [SettingsEntry(Symbol.SlidePlay, nameof(Strings.AppSettingsAutoPlayEntryHeader), nameof(Strings.AppSettingsAutoPlayEntryDescription))]
    public AutoPlaySettings AutoPlay { get; set; } = new();
}
