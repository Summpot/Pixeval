// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using AutoSettingsPage;
using FluentIcons.Common;

namespace Pixeval.Config;

public record FileCacheSettings
{
    public bool LimitFileCacheSize { get; set; }

    [SettingsEntry(Symbol.HardDrive, AppSettingsResources.FileCacheSizeLimitInMegabytesEntry.Header,
        AppSettingsResources.FileCacheSizeLimitInMegabytesEntry.Description)]
    public int FileCacheSizeLimitInMegabytes { get; set; } = 2048;
}
