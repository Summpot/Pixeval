using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement;

namespace Pixeval.Tests;

[TestClass]
public sealed class PixivLoginActivationHubTest
{
    [TestMethod]
    public void RawCodeShouldPassThrough()
    {
        var success = PixivLoginActivationHub.TryExtractCode("raw_auth_code", out var code);

        Assert.IsTrue(success);
        Assert.AreEqual("raw_auth_code", code);
    }

    [TestMethod]
    public void PixivCallbackShouldExtractCode()
    {
        var success = PixivLoginActivationHub.TryExtractCode(
            "pixiv://account/login?code=callback_code&via=login",
            out var code);

        Assert.IsTrue(success);
        Assert.AreEqual("callback_code", code);
    }

    [TestMethod]
    public void NestedRedirectShouldExtractCode()
    {
        var nested = Uri.EscapeDataString("pixiv://account/login?code=nested_code&via=login");
        var success = PixivLoginActivationHub.TryExtractCode(
            $"https://example.com/auth?redirect_uri={nested}",
            out var code);

        Assert.IsTrue(success);
        Assert.AreEqual("nested_code", code);
    }

    [TestMethod]
    public void CallbackUriShouldRequirePixivLoginRoute()
    {
        Assert.IsTrue(PixivLoginActivationHub.TryCreateCallbackUri(
            "pixiv://account/login?code=callback_code",
            out _));

        Assert.IsFalse(PixivLoginActivationHub.TryCreateCallbackUri(
            "pixiv://spotlight/123",
            out _));
    }
}
