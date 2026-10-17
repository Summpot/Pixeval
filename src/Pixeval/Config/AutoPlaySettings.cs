// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using AutoSettingsPage;
using FluentIcons.Common;
using Pixeval.Config;

namespace Pixeval.Config;

public record AutoPlaySettings
{
    [SettingsEntry(Symbol.SlideMultipleArrowRight, nameof(Strings.AppSettingsIllustrationViewerAutoPlayIntervalEntryHeader), nameof(Strings.AppSettingsIllustrationViewerAutoPlayIntervalEntryDescription))]
    public int IllustrationViewerAutoPlayInterval { get; set; } = 5;

    [SettingsEntry(Symbol.ArrowShuffle, nameof(Strings.AppSettingsIllustrationViewerAutoPlayModeEntryHeader), nameof(Strings.AppSettingsIllustrationViewerAutoPlayModeEntryDescription))]
    public IllustrationViewerAutoPlayMode IllustrationViewerAutoPlayMode { get; set; }

    [SettingsEntry(Symbol.ImageMultiple, nameof(Strings.AppSettingsIllustrationViewerAutoPlayScopeEntryHeader), nameof(Strings.AppSettingsIllustrationViewerAutoPlayScopeEntryDescription))]
    public IllustrationViewerAutoPlayScope IllustrationViewerAutoPlayScope { get; set; }
}
