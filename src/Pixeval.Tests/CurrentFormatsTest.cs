using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Config;
using Pixeval.Mako;
using Pixeval.Pixiv;

namespace Pixeval.Tests;

[TestClass]
public sealed class CurrentFormatsTest
{
    [TestMethod]
    public void StoredHomeCardIndicesKeepTheirCurrentMeaning()
    {
        var card = new HomePageCardLayout();
        Assert.AreEqual(WorkKind.Illustration, card.WorkKind);
        Assert.AreEqual(SimpleWorkKind.Illustration, card.SimpleWorkKind);
        Assert.AreEqual(PrivacyRestrict.Public, card.PrivacyRestrict);
        Assert.AreEqual(RankMode.Day, card.RankMode);
        Assert.AreEqual(RankMode.WeekR18, (card with { RawRankOption = 13 }).RankMode);
        Assert.AreEqual(RankMode.WeekAiR18, (card with { RawRankOption = 18 }).RankMode);
        card = card with { RawWorkType = 1, RawSimpleWorkType = 1, RawPrivacyPolicy = 1 };
        Assert.AreEqual(WorkKind.Manga, card.WorkKind);
        Assert.AreEqual(SimpleWorkKind.Novel, card.SimpleWorkKind);
        Assert.AreEqual(PrivacyRestrict.Private, card.PrivacyRestrict);
        var illustration = Pixeval.Common.DesignHelper.DesignIllustration;
        Assert.AreEqual(0, (illustration with { XRestrict = XRestrict.Ordinary }).ToArtworkMetadata().XRestrict);
        Assert.AreEqual(1, (illustration with { XRestrict = XRestrict.R18 }).ToArtworkMetadata().XRestrict);
        Assert.AreEqual(2, (illustration with { XRestrict = XRestrict.R18G }).ToArtworkMetadata().XRestrict);
    }

    [TestMethod]
    public void CurrentSettingsKeepTheirWireValuesAfterNativeEnumSwitch()
    {
        var settings = new AppSettings();
        settings.SearchSettings.DefaultSimpleWorkType = SimpleWorkKind.Novel;
        var expectedWireValues = new Dictionary<RankMode, string>
        {
            [RankMode.Day] = "day", [RankMode.Week] = "week", [RankMode.Month] = "month",
            [RankMode.DayMale] = "day_male", [RankMode.DayFemale] = "day_female",
            [RankMode.DayManga] = "day_manga", [RankMode.WeekManga] = "week_manga", [RankMode.MonthManga] = "month_manga",
            [RankMode.WeekOriginal] = "week_original", [RankMode.WeekRookie] = "week_rookie",
            [RankMode.DayR18] = "day_r18", [RankMode.DayMaleR18] = "day_male_r18", [RankMode.DayFemaleR18] = "day_female_r18",
            [RankMode.WeekR18] = "week_r18", [RankMode.WeekR18G] = "week_r18g",
            [RankMode.DayAi] = "day_ai", [RankMode.DayR18Ai] = "day_r18_ai",
            [RankMode.WeekAi] = "week_ai", [RankMode.WeekAiR18] = "week_ai_r18"
        };
        foreach (var (mode, expected) in expectedWireValues)
        {
            settings.SearchSettings.RankOptions.IllustrationRankOption = mode;
            var json = JsonSerializer.Serialize(settings, SettingsSerializerContext.Default.AppSettings);
            using var document = JsonDocument.Parse(json);
            var search = document.RootElement.GetProperty("SearchSettings");
            Assert.AreEqual("Novel", search.GetProperty("DefaultSimpleWorkType").GetString());
            Assert.AreEqual(expected,
                search.GetProperty("RankOptions").GetProperty("IllustrationRankOption").GetString());
            var restored = JsonSerializer.Deserialize(json, SettingsSerializerContext.Default.AppSettings)!;
            Assert.AreEqual(mode, restored.SearchSettings.RankOptions.IllustrationRankOption);
            Assert.AreEqual(SimpleWorkKind.Novel, restored.SearchSettings.DefaultSimpleWorkType);
        }
    }

    [TestMethod]
    public void CurrentSettingsRejectOldEnumAliasesAndNumbers()
    {
        foreach (var value in new[] { "\"Day\"", "\"DAY\"", "0", "\"unknown\"" })
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize(
                "{\"SearchSettings\":{\"RankOptions\":{\"IllustrationRankOption\":" + value + "}}}",
                SettingsSerializerContext.Default.AppSettings));
    }

    [TestMethod]
    public async Task ProjectedNativeWorkFeedKeepsPageFailureAndRetrySemantics()
    {
        using var client = new MakoClient(Pixeval.Services.Network.ProxyHelper.CreateMakoConfiguration(new PixivDomainFrontingSettings()));
        using var feed = client.WorkRecommendedUnified(WorkKind.Illustration);
        var source = feed.AsObjects();
        Assert.IsInstanceOfType<IPageFeed<object>>(source);
        var pages = (IPageFeed<object>)source;
        await Assert.ThrowsAsync<MakoError>(() => pages.FetchPageAsync());
        Assert.IsFalse(feed.IsCompleted());
        await Assert.ThrowsAsync<MakoError>(() => pages.FetchPageAsync());
    }

    [TestMethod]
    public async Task DirectNativeRequestObservesPreCancellation()
    {
        using var client = new MakoClient(Pixeval.Services.Network.ProxyHelper.CreateMakoConfiguration(new PixivDomainFrontingSettings()));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetIllustrationAsync(1, cancel.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.SearchAutocompleteAsync("test", cancel.Token));
    }
}
