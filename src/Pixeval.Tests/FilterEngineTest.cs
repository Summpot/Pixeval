// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Models.Filters;
using Pixeval.Native.Filters;

namespace Pixeval.Tests;

[TestClass]
public sealed class FilterEngineTest
{
    private static FilterEngine CreateTestEngine()
    {
        var syntaxes = new List<FilterSyntaxDefinition>
        {
            new("Title", FilterValueKind.Text, "keyword",
            [
                new("", [""], "", null, "keyword", "按标题搜索"),
                new("", ["title"], ":", null, "keyword", "按标题搜索")
            ]),
            new("Author", FilterValueKind.Text, "artist",
            [
                new("@", [""], "", null, "artist", "按画师搜索"),
                new("", ["a"], ":", null, "artist", "按画师搜索"),
                new("", ["artist"], ":", null, "artist", "按画师搜索")
            ]),
            new("Tag", FilterValueKind.Text, "tag",
            [
                new("#", [""], "", null, "tag", "按标签搜索"),
                new("", ["t"], ":", null, "tag", "按标签搜索"),
                new("", ["tag"], ":", null, "tag", "按标签搜索")
            ]),
            new("Bookmark", FilterValueKind.LongRange, "100-200",
            [
                new("", ["l"], ":", null, "100-200", "收藏数范围"),
                new("", ["like"], ":", null, "100-200", "收藏数范围")
            ]),
            new("Ai", FilterValueKind.Flag, null,
            [
                new("+", ["ai"], "", "false", null, "仅显示 AI"),
                new("-", ["ai"], "", "true", null, "排除 AI")
            ])
        };

        return new FilterEngine(syntaxes, null, null, null);
    }

    [TestMethod]
    public void CoreFilterEngineShouldAnalyzeQueries()
    {
        using var engine = CreateTestEngine();
        var result = engine.Analyze("#sky @Alice +ai", -1, null);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.HasQuery);
        Assert.IsNotNull(result.QueryHandle);
        Assert.AreEqual(0, result.Diagnostics.Count);
    }

    [TestMethod]
    public void CoreFilterEngineShouldEvaluateArtworkConditions()
    {
        using var engine = CreateTestEngine();
        var result = engine.Analyze("#sky +ai", -1, null);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.QueryHandle);

        var matchingWork = new ArtworkMetadata(
            Id: "1",
            Title: "Blue Sky",
            AuthorName: "Alice",
            AuthorAccount: "alice",
            Tags: [new ArtworkTag("sky", "天空")],
            TotalBookmarks: 200,
            CreateDateTimestamp: 1700000000,
            Width: 1920,
            Height: 1080,
            XRestrict: 0,
            AiType: 1,
            IllustrationType: 0);

        Assert.IsTrue(result.QueryHandle.MatchesArtwork(matchingWork));

        var nonMatchingWork = matchingWork with { AiType = 0 };
        Assert.IsFalse(result.QueryHandle.MatchesArtwork(nonMatchingWork));

        var batchResult = result.QueryHandle.FilterArtworks([matchingWork, nonMatchingWork]);
        Assert.AreEqual(2, batchResult.Count);
        Assert.IsTrue(batchResult[0]);
        Assert.IsFalse(batchResult[1]);
    }

    [TestMethod]
    public void WorkFilterLanguageWrapperShouldAnalyzeAndReconstructAst()
    {
        var language = WorkFilterLanguage.Instance;
        var result = language.Analyze("#sky @Alice +ai");
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Query);
        Assert.AreEqual(0, result.Diagnostics.Count);
    }

    private sealed class MockFilterStorageProvider : IFilterStorageProvider
    {
        public List<TagCandidate> QuerySearchHistoryTags(string pattern, uint limit)
        {
            if ("genshin".Contains(pattern.ToLowerInvariant()))
                return [new TagCandidate("genshin", "原神")];
            return [];
        }

        public List<AuthorCandidate> QuerySubscriptionAuthors(string pattern, uint limit)
        {
            if ("artist".Contains(pattern.ToLowerInvariant()))
                return [new AuthorCandidate("artist_one", "art_acc")];
            return [];
        }
    }

    [TestMethod]
    public void FilterCompletionEngineShouldProvideContextAwareCompletionsAndCompletedText()
    {
        var engine = new FilterCompletionEngine();
        engine.SetSessionCandidates(
            tags: [new ArtworkTag("touhou", "东方"), new ArtworkTag("genshin", null)],
            authors: [new AuthorCandidate("Alice", "alice_acc"), new AuthorCandidate("Bob", null)]);

        // 1. Tag prefix completion
        var tagResult = engine.Analyze("#tou", 4);
        Assert.IsTrue(tagResult.Completions.Count > 0);
        var touhouItem = tagResult.Completions.Find(c => c.DisplayText == "touhou");
        Assert.IsNotNull(touhouItem);
        Assert.AreEqual("touhou", touhouItem.InsertText);
        Assert.AreEqual("#touhou", touhouItem.CompletedText);
        Assert.AreEqual(FilterCompletionKind.Value, touhouItem.Kind);

        // 2. Author prefix completion
        var authorResult = engine.Analyze("@Al", 3);
        Assert.IsTrue(authorResult.Completions.Count > 0);
        var aliceItem = authorResult.Completions.Find(c => c.DisplayText == "Alice");
        Assert.IsNotNull(aliceItem);
        Assert.AreEqual("Alice", aliceItem.InsertText);
        Assert.AreEqual("@Alice", aliceItem.CompletedText);
        Assert.AreEqual(FilterCompletionKind.Value, aliceItem.Kind);

        // 3. Empty input suggestions
        var emptyResult = engine.Analyze("", 0);
        Assert.IsTrue(emptyResult.Completions.Count > 0);
        Assert.IsTrue(emptyResult.Completions.Exists(c => c.DisplayText == "and"));
        Assert.IsTrue(emptyResult.Completions.Exists(c => c.DisplayText == "+ai"));
    }

    [TestMethod]
    public void FilterCompletionEngineShouldIntegrateStorageProvider()
    {
        var provider = new MockFilterStorageProvider();
        var engine = FilterCompletionEngine.WithProvider(provider, null);

        var result = engine.Analyze("#gen", 4);
        Assert.IsTrue(result.Completions.Count > 0);
        var item = result.Completions.Find(c => c.DisplayText == "genshin");
        Assert.IsNotNull(item);
        Assert.AreEqual("genshin", item.InsertText);
        Assert.AreEqual("#genshin", item.CompletedText);
        Assert.AreEqual("原神", item.Description);
    }

    [TestMethod]
    public void FilterCompletionEngineShouldFilterArtworksBatch()
    {
        var engine = new FilterCompletionEngine();
        var matchingWork = new ArtworkMetadata(
            Id: "1",
            Title: "Blue Sky",
            AuthorName: "Alice",
            AuthorAccount: "alice",
            Tags: [new ArtworkTag("sky", "天空")],
            TotalBookmarks: 200,
            CreateDateTimestamp: 1700000000,
            Width: 1920,
            Height: 1080,
            XRestrict: 0,
            AiType: 1,
            IllustrationType: 0);
        var nonMatchingWork = matchingWork with { AiType = 0 };

        var matches = engine.FilterArtworks("#sky +ai", [matchingWork, nonMatchingWork]);
        Assert.AreEqual(2, matches.Count);
        Assert.IsTrue(matches[0]);
        Assert.IsFalse(matches[1]);
    }
}
