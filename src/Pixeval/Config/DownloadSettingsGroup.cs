// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Text.Json.Serialization;
using AutoSettingsPage;
using FluentIcons.Common;

namespace Pixeval.Config;

public record DownloadSettingsGroup
{
    [SettingsEntry(Symbol.Rename, nameof(Strings.AppSettingsDownloadPathMacroEntryHeader), nameof(Strings.AppSettingsDownloadPathMacroEntryDescription))]
    public string DownloadPathMacro { get; set; } = Path.Join(
        GetSpecialFolder(),
        "@{is_group?@{group_id}:}",
        "@{is_pic_set?[@{artist_name}] @{title}:}",
        "[@{artist_name}] @{id}@{is_pic_set?p@{pic_set_index}:}.@{ext}"
    );

    [SettingsEntry(Symbol.TextPeriodAsterisk, nameof(Strings.AppSettingsWorkDownloadFormatEntryHeader), nameof(Strings.AppSettingsWorkDownloadFormatEntryDescription))]
    public DownloadFormatsSettings DownloadFormats { get; set; } = new();

    [SettingsEntry(Symbol.ImageSplit, nameof(Strings.AppSettingsOverwriteDownloadedFileEntryHeader), nameof(Strings.AppSettingsOverwriteDownloadedFileEntryDescription))]
    public bool OverwriteDownloadedFile { get; set; }

    /// <summary>
    /// The max download tasks that are allowed to run concurrently
    /// </summary>
    [SettingsEntry(Symbol.DeveloperBoardLightning, nameof(Strings.AppSettingsMaxDownloadConcurrencyLevelEntryHeader), nameof(Strings.AppSettingsMaxDownloadConcurrencyLevelEntryDescription))]
    public int MaxDownloadTaskConcurrencyLevel { get; set; } = Environment.ProcessorCount / 4;

    [SettingsEntry(Symbol.Clock, nameof(Strings.AppSettingsEnableSubscriptionDaemonEntryHeader), nameof(Strings.AppSettingsEnableSubscriptionDaemonEntryDescription))]
    public bool EnableSubscriptionDaemon { get; set; } = true;

    [SettingsEntry(Symbol.Timer, nameof(Strings.AppSettingsSubscriptionDaemonIntervalMinutesEntryHeader), nameof(Strings.AppSettingsSubscriptionDaemonIntervalMinutesEntryDescription))]
    public int SubscriptionDaemonIntervalMinutes { get; set; } = 30;

    [JsonIgnore]
    [SettingsEntry(Symbol.FolderSync, nameof(Strings.AppSettingsWorkSubscriptionsSettingsEntryHeader), nameof(Strings.AppSettingsWorkSubscriptionsSettingsEntryDescription))]
    public byte WorkSubscriptions => 0;

    private static string GetSpecialFolder()
    {
        var picPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures, Environment.SpecialFolderOption.None);
        var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.None);
        var picDirectory = Path.GetDirectoryName(picPath);
        return picDirectory == Path.GetDirectoryName(docPath)
            ? Path.Join(picDirectory!,
                $"@{{is_novel?{Path.GetFileName(docPath)}:{Path.GetFileName(picPath)}}}")
            : $"@{{is_novel?{docPath}:{picPath}}}";
    }
}
