// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Maho;

namespace Pixeval.Tests;

[TestClass]
public sealed class PixivNetworkServiceTest
{
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
