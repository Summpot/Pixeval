// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Options;
using Pixeval.Native.Mako;
using Pixeval.Utilities.Network;

namespace Pixeval.Tests;

[TestClass]
public sealed class PixivNetworkServiceTest
{
    [TestMethod]
    public void PixivArtworkServiceShouldConfigureDefaultImageHeaders()
    {
        var settings = new PixivDomainFrontingSettings();
        var makoConfig = Utilities.MakoHelper.CreateMakoConfiguration(settings, 500);
        using var makoClient = new MakoClient(makoConfig);
        using var mahoTransport = new MahoTransport();
        using var service = new PixivArtworkService(makoClient, mahoTransport);

        var client1 = service.GetImageDownloadClient();
        var client2 = service.GetImageDownloadClient();

        Assert.AreSame(client1, client2);
        Assert.AreEqual(new Uri("https://app-api.pixiv.net/"), client1.DefaultRequestHeaders.Referrer);
        Assert.IsTrue(client1.DefaultRequestHeaders.UserAgent.ToString().Contains("PixivAndroidApp"));

        service.Reset();
        var client3 = service.GetImageDownloadClient();
        Assert.AreNotSame(client1, client3);
        Assert.AreEqual(new Uri("https://app-api.pixiv.net/"), client3.DefaultRequestHeaders.Referrer);
    }

    [TestMethod]
    public void PixivDirectProxyShouldBypassWhenDomainFrontingEnabled()
    {
        var networkSettings = new NetworkSettingsGroup
        {
            ProxySettings = new()
            {
                ProxyType = ProxyType.Custom,
                Proxy = "http://127.0.0.1:7890"
            },
            PixivDomainFronting = new()
            {
                EnablePixivDomainFronting = true,
                PixivImageNameResolver = ["210.140.139.134"]
            }
        };

        var proxy = PixivDirectProxy.Create(networkSettings);

        // Host with resolver should bypass proxy
        Assert.IsTrue(proxy.IsBypassed(new Uri("https://i.pximg.net/c/240x480/img.jpg")));

        // Host without domain fronting should not bypass proxy
        Assert.IsFalse(proxy.IsBypassed(new Uri("https://other-domain.org/image.png")));
        var proxyUri = proxy.GetProxy(new Uri("https://other-domain.org/image.png"));
        Assert.AreEqual(new Uri("http://127.0.0.1:7890"), proxyUri);
    }

    [TestMethod]
    public void PixivDirectProxyShouldUseProxyWhenDomainFrontingDisabled()
    {
        var networkSettings = new NetworkSettingsGroup
        {
            ProxySettings = new()
            {
                ProxyType = ProxyType.Custom,
                Proxy = "http://127.0.0.1:7890"
            },
            PixivDomainFronting = new()
            {
                EnablePixivDomainFronting = false,
                PixivImageNameResolver = ["210.140.139.134"]
            }
        };

        var proxy = PixivDirectProxy.Create(networkSettings);

        // When domain fronting is disabled, all destinations follow proxy settings
        Assert.IsFalse(proxy.IsBypassed(new Uri("https://i.pximg.net/c/240x480/img.jpg")));
        var proxyUri = proxy.GetProxy(new Uri("https://i.pximg.net/c/240x480/img.jpg"));
        Assert.AreEqual(new Uri("http://127.0.0.1:7890"), proxyUri);
    }

    [TestMethod]
    public void IsPixivHostShouldIdentifyPixivDomains()
    {
        Assert.IsTrue(MahoSocketsHttpHandlerFactory.IsPixivHost("i.pximg.net"));
        Assert.IsTrue(MahoSocketsHttpHandlerFactory.IsPixivHost("s.pximg.net"));
        Assert.IsTrue(MahoSocketsHttpHandlerFactory.IsPixivHost("app-api.pixiv.net"));
        Assert.IsTrue(MahoSocketsHttpHandlerFactory.IsPixivHost("pixiv.net"));
        Assert.IsTrue(MahoSocketsHttpHandlerFactory.IsPixivHost("pximg.net"));

        Assert.IsFalse(MahoSocketsHttpHandlerFactory.IsPixivHost("127.0.0.1"));
        Assert.IsFalse(MahoSocketsHttpHandlerFactory.IsPixivHost("github.com"));
        Assert.IsFalse(MahoSocketsHttpHandlerFactory.IsPixivHost("example.com"));
    }
}
