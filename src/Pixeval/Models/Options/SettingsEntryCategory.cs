// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Models.Options;

[LocalizationMetadata]
public enum SettingsEntryCategory
{
    [LocalizedResource(SettingsPageResources.VersionSettingsGroupText)]
    Version,

    [LocalizedResource(SettingsPageResources.SessionSettingsGroupText)]
    Session,

    [LocalizedResource(SettingsPageResources.ApplicationSettingsGroupText)]
    Application,

    [LocalizedResource(SettingsPageResources.BrowsingExperienceSettingsGroupText)]
    BrowsingExperience,

    [LocalizedResource(SettingsPageResources.SearchSettingsGroupText)]
    Search,

    [LocalizedResource(SettingsPageResources.DownloadSettingsGroupText)]
    Download,

    [LocalizedResource(SettingsPageResources.MiscSettingsGroupText)]
    Misc
}
