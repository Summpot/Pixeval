using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Filters;
using Pixeval.Models.Filters;
using Pixeval.Native.Filters;

namespace Pixeval.Tests;

[TestClass]
public sealed class WorkFilterNodeTest
{
    private static readonly FilterLanguage _Language = WorkFilterLanguage.Instance;

    [TestMethod]
    public void WorkFilterNodesShouldSupportAllPredicatesAndGroups()
    {
        var work = CreateIllustration(
            title: "Blue Hour",
            author: "Alice",
            tags:
            [
                new("sky", "空")
            ],
            totalBookmarks: 180,
            createDate: new(2024, 2, 3, 0, 0, 0, TimeSpan.Zero),
            width: 1200,
            height: 600,
            xRestrict: 1,
            aiType: 1,
            illustrationType: 2);

        AssertMatches(work, "Blue");
        AssertMatches(work, "@Alice");
        AssertMatches(work, "#空");
        AssertMatches(work, "like:100-200");
        AssertMatches(work, "ratio:1-3");
        AssertMatches(work, "start:2024-1-1");
        AssertMatches(work, "end:2024-12-31");
        AssertMatches(work, "+r18");
        AssertMatches(work, "+ai");
        AssertMatches(work, "+gif");
        AssertMatches(work with { XRestrict = 2 }, "+r18g");
        AssertMatches(work, "(and Blue @Alice)");
        AssertMatches(work, "(or Red @Alice)");
        AssertMatches(work, "!Red");
        AssertDoesNotMatch(work, "(and Blue @Bob)");
        AssertDoesNotMatch(work, "!(or Blue @Alice)");
    }

    [TestMethod]
    public void WorkFilterNodesShouldRejectNonMatchingPredicates()
    {
        var work = CreateIllustration(
            title: "Blue Hour",
            author: "Alice",
            tags:
            [
                new("sky", null)
            ],
            totalBookmarks: 180,
            createDate: new(2024, 2, 3, 0, 0, 0, TimeSpan.Zero),
            width: 1200,
            height: 600,
            xRestrict: 0,
            aiType: 0,
            illustrationType: 0);

        AssertDoesNotMatch(work, "Red");
        AssertDoesNotMatch(work, "@Bob");
        AssertDoesNotMatch(work, "#sea");
        AssertDoesNotMatch(work, "like:200-300");
        AssertDoesNotMatch(work, "ratio:3-4");
        AssertDoesNotMatch(work, "+r18");
        AssertDoesNotMatch(work, "+ai");
        AssertDoesNotMatch(work, "+gif");
    }

    private static void AssertMatches(ArtworkMetadata work, string text) =>
        Assert.IsTrue(Parse(text).MatchesArtwork(work), text);

    private static void AssertDoesNotMatch(ArtworkMetadata work, string text) =>
        Assert.IsFalse(Parse(text).MatchesArtwork(work), text);

    private static FilterQuery Parse(string text)
    {
        var analysis = _Language.Analyze(text);
        Assert.IsTrue(analysis.IsSuccess, text);
        return analysis.Query!;
    }

    private static ArtworkMetadata CreateIllustration(
        string title,
        string author,
        IReadOnlyList<ArtworkTag> tags,
        int totalBookmarks,
        DateTimeOffset createDate,
        int width,
        int height,
        int xRestrict,
        int aiType,
        int illustrationType) =>
        new(
            "100",
            title,
            author,
            author.ToLowerInvariant(),
            tags.ToList(),
            totalBookmarks,
            createDate.ToUnixTimeSeconds(),
            width,
            height,
            xRestrict,
            aiType,
            illustrationType);
}
