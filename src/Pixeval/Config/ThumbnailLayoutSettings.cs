// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using AutoSettingsPage;
using FluentIcons.Common;
using Pixeval.Config;

namespace Pixeval.Config;

public record ThumbnailLayoutSettings
{
    public ThumbnailLayoutType ThumbnailLayoutType { get; set; } = ThumbnailLayoutType.LinedFlow;

    [SettingsEntry(Symbol.AutoFitHeight, nameof(Strings.AppSettingsIllustrationLinedFlowItemHeightEntryHeader), nameof(Strings.AppSettingsIllustrationLinedFlowItemHeightEntryDescription))]
    public int IllustrationLinedFlowItemHeight { get; set; } = 200;

    [SettingsEntry(Symbol.AutoFitWidth, nameof(Strings.AppSettingsIllustrationGridItemSizeEntryHeader), nameof(Strings.AppSettingsIllustrationGridItemSizeEntryDescription))]
    public int IllustrationGridItemSize { get; set; } = 150;

    [SettingsEntry(Symbol.AutoFitHeight, nameof(Strings.AppSettingsIllustrationGridLineSizeEntryHeader), nameof(Strings.AppSettingsIllustrationGridLineSizeEntryDescription))]
    public int IllustrationGridLineSize { get; set; } = 200;

    [SettingsEntry(Symbol.AutoFitWidth, nameof(Strings.AppSettingsIllustrationMasonryColumnWidthEntryHeader), nameof(Strings.AppSettingsIllustrationMasonryColumnWidthEntryDescription))]
    public int IllustrationMasonryColumnWidth { get; set; } = 250;
}
