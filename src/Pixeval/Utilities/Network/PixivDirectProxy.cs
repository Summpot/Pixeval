// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Net;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Options;

namespace Pixeval.Utilities.Network;

internal sealed class PixivDirectProxy(NetworkSettingsGroup settings) : IWebProxy
{
    public ICredentials? Credentials { get; set; }

    public static IWebProxy Create(NetworkSettingsGroup settings) => new PixivDirectProxy(settings);

    public Uri GetProxy(Uri destination)
    {
        if (IsBypassed(destination))
            return destination;

        return settings.ProxySettings.ProxyType switch
        {
            ProxyType.System => SystemProxyProvider.GetCurrent().GetProxy(destination) ?? destination,
            ProxyType.Custom => CreateExplicitProxy()?.GetProxy(destination) ?? destination,
            _ => destination
        };
    }

    public bool IsBypassed(Uri host)
    {
        if (settings.PixivDomainFronting.EnablePixivDomainFronting &&
            HasConfiguredResolver(settings, host.Host))
        {
            return true;
        }

        return settings.ProxySettings.ProxyType switch
        {
            ProxyType.None => true,
            ProxyType.System => SystemProxyProvider.GetCurrent().IsBypassed(host),
            ProxyType.Custom => CreateExplicitProxy() is not { } proxy || proxy.IsBypassed(host),
            _ => true
        };
    }

    public static bool HasConfiguredResolver(NetworkSettingsGroup settings, string host)
    {
        if (!settings.PixivDomainFronting.EnablePixivDomainFronting)
            return false;

        var df = settings.PixivDomainFronting;
        return (host.Equals(MakoHelper.AppApiHost, StringComparison.OrdinalIgnoreCase) && df.PixivAppApiNameResolver.Count > 0)
            || (host.Equals(MakoHelper.ImageHost, StringComparison.OrdinalIgnoreCase) && df.PixivImageNameResolver.Count > 0)
            || (host.Equals(MakoHelper.ImageHost2, StringComparison.OrdinalIgnoreCase) && df.PixivImageNameResolver2.Count > 0)
            || (host.Equals(MakoHelper.OAuthHost, StringComparison.OrdinalIgnoreCase) && df.PixivOAuthNameResolver.Count > 0)
            || (host.Equals(MakoHelper.AccountHost, StringComparison.OrdinalIgnoreCase) && df.PixivAccountNameResolver.Count > 0)
            || (host.Equals(MakoHelper.WebApiHost, StringComparison.OrdinalIgnoreCase) && df.PixivWebApiNameResolver.Count > 0);
    }

    private WebProxy? CreateExplicitProxy()
    {
        if (MakoHelper.NormalizeProxyUri(settings.ProxySettings.Proxy) is not { } proxyUri ||
            !Uri.TryCreate(proxyUri, UriKind.Absolute, out var uri))
            return null;

        if (settings.ProxySettings.ProxyType is not ProxyType.Custom)
            return null;

        var proxy = new WebProxy(uri)
        {
            Credentials = Credentials
        };
        return proxy;
    }
}
