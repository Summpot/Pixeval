// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using AutoSettingsPage;
using FluentIcons.Common;

namespace Pixeval.Config;

public record McpSettingsGroup
{
    public const ushort DefaultPort = 52163;

    // BlobResourceContents is backed by a byte[], so keep the UI limit inside that boundary.
    public const int MaxBinaryResourceMegabytesLimit = 2047;

    [SettingsEntry(Symbol.ServerPlay, nameof(Strings.AppSettingsEnableMcpServerEntryHeader), nameof(Strings.AppSettingsEnableMcpServerEntryDescription))]
    public bool EnableServer { get; set; }

    [SettingsEntry(Symbol.SerialPort, nameof(Strings.AppSettingsMcpPortEntryHeader), nameof(Strings.AppSettingsMcpPortEntryDescription))]
    public ushort Port { get; set; } = DefaultPort;

    [SettingsEntry(Symbol.CalligraphyPenCheckmark, nameof(Strings.AppSettingsEnableMcpWriteToolsEntryHeader), nameof(Strings.AppSettingsEnableMcpWriteToolsEntryDescription))]
    public bool EnableWriteTools { get; set; }

    [SettingsEntry(Symbol.DatabaseCheckmark, nameof(Strings.AppSettingsMcpMaxBinaryResourceMegabytesEntryHeader),
        nameof(Strings.AppSettingsMcpMaxBinaryResourceMegabytesEntryDescription))]
    public int MaxBinaryResourceMegabytes { get; set; } = 50;
}
