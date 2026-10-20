// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using AutoSettingsPage;
using FluentIcons.Common;
using Pixeval.Config;
using Pixeval.Utilities;
using Pixeval.Services.Network;

namespace Pixeval.Config;

public record AppSettings
{
    public void Initialize()
    {
        if (NetworkSettings.GitHubDomainFronting.GitHubAssetsNameResolver is
            [
                "185.199.108.154",
                "185.199.109.154",
                "185.199.110.154",
                "185.199.111.154"
            ])
        {
            NetworkSettings.GitHubDomainFronting.GitHubAssetsNameResolver = new NetworkSettingsGroup().GitHubDomainFronting.GitHubAssetsNameResolver;
        }

        if (LastOpenedVersion != AppInfo.AppVersion.CurrentVersionShortText)
        {
            IsNewVersion = true;
            LastOpenedVersion = AppInfo.AppVersion.CurrentVersionShortText;

        }
    }

    [SettingsEntry(Symbol.Apps, nameof(Strings.AppSettingsSettingsGroupApplicationHeader), null)]
    public ApplicationSettingsGroup ApplicationSettings { get; set; } = new();

    [SettingsEntry(Symbol.WiFi, nameof(Strings.AppSettingsSettingsGroupNetworkHeader), null)]
    public NetworkSettingsGroup NetworkSettings { get; set; } = new();

    [SettingsEntry(Symbol.News, nameof(Strings.AppSettingsSettingsGroupBrowsingExperienceHeader), null)]
    public BrowsingExperienceSettingsGroup BrowsingExperienceSettings { get; set; } = new();

    [SettingsEntry(Symbol.SearchSparkle, nameof(Strings.AppSettingsSettingsGroupSearchHeader), null)]
    public SearchSettingsGroup SearchSettings { get; set; } = new();

    [SettingsEntry(Symbol.ArrowSquareDown, nameof(Strings.AppSettingsSettingsGroupDownloadHeader), null)]
    public DownloadSettingsGroup DownloadSettings { get; set; } = new();

#if PIXEVAL_MCP
    [SettingsEntry(Symbol.Bot, nameof(Strings.AppSettingsSettingsGroupMcpHeader), null)]
    public McpSettingsGroup McpSettings { get; set; } = new();
#endif

    [SettingsEntry(Symbol.Settings, nameof(Strings.AppSettingsSettingsGroupNovelHeader), null)]
    public NovelSettingsGroup NovelSettings { get; set; } = new();

    /// <summary>
    /// <see cref="object"/> 只能是基元类型或 <see cref="SettingsSerializerContext"/> 提供过的类型
    /// </summary>
    public Dictionary<string, Dictionary<string, object?>> ExtensionSettings { get; set; } = [];

    /// <summary>
    /// 相对于 <see cref="AppInfo.ExtensionsFolder"/> 的待卸载扩展文件或目录路径。
    /// </summary>
    public HashSet<string> PendingExtensionUninstallTargets { get; set; } = [];

    public string LastOpenedVersion { get; set; } = "";

    [JsonIgnore]
    public bool IsNewVersion { get; private set; }

    public MakoConfigurationDto ToMakoConfiguration()
    {
        return ProxyHelper.CreateMakoConfiguration(
            NetworkSettings.PixivDomainFronting,
            NetworkSettings.ApiRequestCooldown,
            proxyUrl: ProxyHelper.GetEffectiveProxyUrl(NetworkSettings),
            targetFilter: BrowsingExperienceSettings.TargetFilter switch
            {
                Pixiv.TargetFilter.ForIos => "for_ios",
                _ => "for_android"
            },
            mirrorHost: NetworkSettings.MirrorHost,
            webCookie: NetworkSettings.WebCookie);
    }
}
