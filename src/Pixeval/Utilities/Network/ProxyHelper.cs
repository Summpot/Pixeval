// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;

namespace Pixeval.Utilities.Network;

public static class ProxyHelper
{
    public static MakoConfigurationDto CreateMakoConfiguration(
        PixivDomainFrontingSettings domainFronting,
        int cooldownMs = 700,
        ulong splitDelayMs = 100,
        string? proxyUrl = null,
        string? targetFilter = "for_android",
        string? mirrorHost = null,
        string? webCookie = null)
    {
        var mappings = new Dictionary<string, List<string>>
        {
            [MakoHttpOptions.AppApiHost] = [.. domainFronting.PixivAppApiNameResolver],
            [MakoHttpOptions.OAuthHost] = [.. domainFronting.PixivOAuthNameResolver],
            [MakoHttpOptions.WebApiHost] = [.. domainFronting.PixivWebApiNameResolver],
            [MakoHttpOptions.AccountHost] = [.. domainFronting.PixivAccountNameResolver],
            [MakoHttpOptions.ImageHost] = [.. domainFronting.PixivImageNameResolver],
            [MakoHttpOptions.ImageHost2] = [.. domainFronting.PixivImageNameResolver2]
        };

        return new MakoConfigurationDto(
            domainFronting.EnablePixivDomainFronting,
            (ulong) Math.Max(0, cooldownMs),
            splitDelayMs,
            mappings,
            proxyUrl,
            targetFilter,
            mirrorHost,
            webCookie);
    }

    public static string? ToMakoProxy(ProxyType type, string? proxy) =>
        type switch
        {
            ProxyType.System => null,
            ProxyType.Custom => NormalizeProxyUri(proxy) ?? "",
            ProxyType.None => "",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    public static string? NormalizeProxyUri(string? proxy)
    {
        if (string.IsNullOrWhiteSpace(proxy))
            return null;

        var uri = proxy.Trim();
        if (!uri.Contains("://", StringComparison.Ordinal))
            uri = "http://" + uri;

        return Uri.IsWellFormedUriString(uri, UriKind.Absolute) ? uri : null;
    }

    public static string? GetEffectiveProxyUrl() =>
        GetEffectiveProxyUrl(App.Services?.GetService<AppSettings>()?.NetworkSettings);

    public static string? GetEffectiveProxyUrl(NetworkSettingsGroup? networkSettings)
    {
        if (networkSettings is null)
            return null;

        switch (networkSettings.ProxySettings.ProxyType)
        {
            case ProxyType.Custom:
                return NormalizeProxyUri(networkSettings.ProxySettings.Proxy);
            case ProxyType.System:
                try
                {
                    var probeUri = new Uri("https://app-api.pixiv.net");
                    var systemProxy = SystemProxyProvider.GetCurrent();
                    if (!systemProxy.IsBypassed(probeUri))
                    {
                        var proxy = systemProxy.GetProxy(probeUri);
                        if (proxy is not null && proxy != probeUri)
                        {
                            return proxy.AbsoluteUri;
                        }
                    }
                }
                catch
                {
                    try
                    {
                        var probeUri = new Uri("https://app-api.pixiv.net");
                        var defaultProxy = System.Net.Http.HttpClient.DefaultProxy;
                        if (!defaultProxy.IsBypassed(probeUri))
                        {
                            var proxy = defaultProxy.GetProxy(probeUri);
                            if (proxy is not null && proxy != probeUri)
                            {
                                return proxy.AbsoluteUri;
                            }
                        }
                    }
                    catch
                    {
                        // ignore proxy resolution failures
                    }
                }
                return null;
            case ProxyType.None:
            default:
                return null;
        }
    }
}
