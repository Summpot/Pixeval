// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using AutoSettingsPage;
using FluentIcons.Common;

namespace Pixeval.Config;

public record NetworkSettingsGroup
{
    [SettingsEntry(Symbol.Timer, nameof(Strings.AppSettingsApiRequestCooldownEntryHeader), nameof(Strings.AppSettingsApiRequestCooldownEntryDescription))]
    public int ApiRequestCooldown { get; set; } = 700;

    [SettingsEntry(Symbol.Router, nameof(Strings.AppSettingsProxyTypeEntryHeader), nameof(Strings.AppSettingsProxyTypeEntryDescription))]
    public ProxySettings ProxySettings { get; set; } = new();

    [SettingsEntry(Symbol.ShieldTask, nameof(Strings.AppSettingsEnableGitHubDomainFrontingEntryHeader), nameof(Strings.AppSettingsEnableGitHubDomainFrontingEntryDescription))]
    public GitHubDomainFrontingSettings GitHubDomainFronting { get; set; } = new();

    [SettingsEntry(Symbol.ShieldTask, nameof(Strings.AppSettingsEnablePixivDomainFrontingEntryHeader), nameof(Strings.AppSettingsEnablePixivDomainFrontingEntryDescription))]
    public PixivDomainFrontingSettings PixivDomainFronting { get; set; } = new();

    /// <summary>
    /// The mirror host for image server, Pixeval will do a simple substitution that
    /// changes the host of the original url(i.pximg.net) to this one.
    /// </summary>
    [SettingsEntry(Symbol.HardDrive, nameof(Strings.AppSettingsImageMirrorServerEntryHeader), nameof(Strings.AppSettingsImageMirrorServerEntryDescription), nameof(Strings.AppSettingsImageMirrorServerEntryPlaceholder))]
    public string MirrorHost { get; set; } = "";

    [SettingsEntry(Symbol.Cookies, nameof(Strings.AppSettingsWebCookieEntryHeader), nameof(Strings.AppSettingsWebCookieEntryDescription), nameof(Strings.AppSettingsWebCookieEntryPlaceholder))]
    public string WebCookie { get; set; } = "";
}
