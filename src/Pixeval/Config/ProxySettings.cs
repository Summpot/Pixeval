// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using AutoSettingsPage;
using FluentIcons.Common;
using Pixeval.Config;

namespace Pixeval.Config;

public record ProxySettings
{
    public ProxyType ProxyType { get; set; }

    [SettingsEntry(Symbol.Server, nameof(Strings.AppSettingsProxyTextBoxEntryHeader), nameof(Strings.AppSettingsProxyTextBoxEntryDescription))]
    public string Proxy { get; set; } = "";
}
