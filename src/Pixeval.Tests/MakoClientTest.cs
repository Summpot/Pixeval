// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;
using Pixeval.Utilities.Network;
using Pixeval.Native.Mako;

namespace Pixeval.Tests;

[TestClass]
public sealed class MakoClientTest
{
    private static MakoClient CreateTestClient(PixivDomainFrontingSettings settings)
    {
        var config = ProxyHelper.CreateMakoConfiguration(settings);
        return new MakoClient(config);
    }

    [TestMethod]
    public void MakoClientLifecycleShouldWork()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);

        Assert.IsNull(client.GetUser());

        client.SetRefreshToken("mock_token");
        client.ClearToken();

        Assert.IsNull(client.GetUser());
    }

    [TestMethod]
    public async Task MakoClientFetchEngineShouldImplementAsyncEnumerable()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);

        using var engine = client.WorkRecommended(includeRanking: true, includePrivacy: true);

        // Verify that IllustrationFetchEngine implements IAsyncEnumerable<Illustration>
        Assert.IsInstanceOfType<IAsyncEnumerable<Illustration>>(engine);

        // Engine starts with 0 requested pages
        Assert.AreEqual(0u, engine.RequestedPages());

        // Test cancel
        engine.Cancel();

        var count = 0;
        await foreach (var item in engine)
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task MakoClientNovelEngineShouldImplementAsyncEnumerable()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);

        using var engine = client.NovelRecommended(includeRanking: true, includePrivacy: true);

        // Verify that NovelFetchEngine implements IAsyncEnumerable<Novel>
        Assert.IsInstanceOfType<IAsyncEnumerable<Novel>>(engine);

        Assert.AreEqual(0u, engine.RequestedPages());

        engine.Cancel();

        var count = 0;
        await foreach (var item in engine)
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task MakoClientWorkSearchEngineShouldImplementAsyncEnumerable()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);

        using var engine = client.WorkSearch("illust", "test", null, null);

        // Verify that WorkFetchEngine implements IAsyncEnumerable<WorkEntry>
        Assert.IsInstanceOfType<IAsyncEnumerable<WorkEntry>>(engine);

        Assert.AreEqual(0u, engine.RequestedPages());

        engine.Cancel();

        var count = 0;
        await foreach (var item in engine)
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task MakoClientUserEngineShouldImplementAsyncEnumerable()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);

        using var engine = client.UserRecommended();

        // Verify that UserFetchEngine implements IAsyncEnumerable<User>
        Assert.IsInstanceOfType<IAsyncEnumerable<User>>(engine);

        Assert.AreEqual(0u, engine.RequestedPages());

        engine.Cancel();

        var count = 0;
        await foreach (var item in engine)
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void MakoClientCreateConfigurationShouldPreserveResolvers()
    {
        var settings = new PixivDomainFrontingSettings();
        var config = ProxyHelper.CreateMakoConfiguration(settings, cooldownMs: 800, splitDelayMs: 150, proxyUrl: "http://127.0.0.1:7890");

        Assert.IsTrue(config.DomainFrontingEnabled);
        Assert.AreEqual(800ul, config.CooldownMs);
        Assert.AreEqual(150ul, config.SplitDelayMs);
        Assert.AreEqual("http://127.0.0.1:7890", config.ProxyUrl);
        Assert.IsTrue(config.HostIps.ContainsKey(MakoHttpOptions.AppApiHost));
        Assert.IsTrue(config.HostIps[MakoHttpOptions.AppApiHost].Count > 0);
    }

    [TestMethod]
    public void MakoClientUpdateConfigurationShouldWork()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);
        var updatedConfig = ProxyHelper.CreateMakoConfiguration(settings, cooldownMs: 500, splitDelayMs: 80, proxyUrl: "http://127.0.0.1:1080");
        client.UpdateConfiguration(updatedConfig);
    }

    [TestMethod]
    public async Task IdentifyTokenTest()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);
        client.SetRefreshToken("invalid_token");
        var res = await client.IdentifyTokenAsync();
        Assert.IsFalse(res.Success);
    }

    [TestMethod]
    public void MakoClientUserSessionManagementShouldWork()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);
        Assert.IsNull(client.GetUser());
        Assert.IsNull(client.GetTokenResponse());

        var user = new TokenUser("123456", "Test User", "test_user", "test@example.com", false, new ProfileImageUrls("url16", "url50", "url170", null));
        client.SetRefreshToken("test_refresh_token");
        client.SetUser(user);

        var retrievedUser = client.GetUser();
        Assert.IsNotNull(retrievedUser);
        Assert.AreEqual("123456", retrievedUser.Id);
        Assert.AreEqual("Test User", retrievedUser.Name);
        Assert.AreEqual("test_refresh_token", client.GetRefreshToken());

        var tokenResp = client.GetTokenResponse();
        Assert.IsNotNull(tokenResp);
        Assert.AreEqual("test_refresh_token", tokenResp.RefreshToken);
        Assert.IsNotNull(tokenResp.User);
        Assert.AreEqual("Test User", tokenResp.User.Name);

        client.ClearToken();
        Assert.IsNull(client.GetUser());
        Assert.IsNull(client.GetTokenResponse());
    }

    [TestMethod]
    public async Task MakoClientSpotlightEngineShouldImplementAsyncEnumerable()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);

        using var engine = client.SpotlightArticles("all");

        // Verify that SpotlightFetchEngine implements IAsyncEnumerable<SpotlightArticle>
        Assert.IsInstanceOfType<IAsyncEnumerable<SpotlightArticle>>(engine);

        Assert.AreEqual(0u, engine.RequestedPages());

        engine.Cancel();

        var count = 0;
        await foreach (var item in engine)
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void MakoSpotlightShouldReturnFetchEngineWithMapping()
    {
        var settings = new PixivDomainFrontingSettings();
        using var client = CreateTestClient(settings);

        var spotlightStream = client.Spotlight("all");
        Assert.IsInstanceOfType<Pixeval.Models.Pixiv.IFetchEngine<SpotlightArticle>>(spotlightStream);

        // Verify native SpotlightArticle extensions
        var article = new SpotlightArticle(
            12345,
            "Spotlight Title",
            "Pure Title",
            "https://example.com/thumb.jpg",
            "https://example.com/article",
            "2026-10-06T10:00:00+09:00",
            "spotlight",
            "Subcategory"
        );
        Assert.AreEqual(12345L, article.Id);
        Assert.AreEqual("Spotlight Title", article.Title);
        Assert.AreEqual("Pure Title", article.PureTitle);
        Assert.AreEqual("https://example.com/thumb.jpg", article.Thumbnail);
        Assert.AreEqual("https://example.com/article", article.ArticleUrl);
        Assert.AreEqual(Pixeval.Models.Pixiv.SpotlightCategory.Spotlight, article.CategoryEnum);
        Assert.AreEqual("Subcategory", article.SubcategoryLabel);
        Assert.AreEqual(new System.Uri("https://www.pixivision.net/a/12345"), article.WebsiteUri);
        Assert.AreEqual(new System.Uri("pixeval://spotlight/12345"), article.AppUri);
    }
}
