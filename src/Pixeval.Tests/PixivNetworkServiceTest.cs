// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement.Settings;
using Pixeval.Native.Maho;
using Pixeval.Native.Mako;
using Pixeval.Utilities.Network;

namespace Pixeval.Tests;

[TestClass]
public sealed class PixivNetworkServiceTest
{
    [TestMethod]
    public void PixivArtworkServiceShouldExposePixivPlatform()
    {
        var settings = new PixivDomainFrontingSettings();
        var makoConfig = ProxyHelper.CreateMakoConfiguration(settings, 500);
        using var makoClient = new MakoClient(makoConfig);
        var service = new PixivArtworkService(makoClient);

        Assert.AreEqual(Pixeval.Models.PlatformConstants.Pixiv, service.Platform);
    }

    [TestMethod]
    public void NativeIsPixivHostShouldIdentifyPixivDomains()
    {
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("i.pximg.net"));
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("s.pximg.net"));
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("app-api.pixiv.net"));
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("pixiv.net"));
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("pximg.net"));

        Assert.IsFalse(PixevalMahoMethods.IsPixivHost("127.0.0.1"));
        Assert.IsFalse(PixevalMahoMethods.IsPixivHost("github.com"));
        Assert.IsFalse(PixevalMahoMethods.IsPixivHost("example.com"));
    }
}
