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
}
