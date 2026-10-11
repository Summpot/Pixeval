// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.ViewModels.Search;

namespace Pixeval.Tests;

[TestClass]
public class SearchCompletionPureFunctionsTest
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow("word", "word")]
    [DataRow("tag1 tag2", "tag2")]
    [DataRow("tag1 tag2 ", "")]
    [DataRow("a b c", "c")]
    public void GetLastKeyword_ReturnsExpectedTail(string input, string expected)
    {
        var actual = SearchPageViewModel.GetLastKeyword(input);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("", "tag", "tag")]
    [DataRow("original", "replacement", "replacement")]
    [DataRow("first second", "completed", "first completed")]
    [DataRow("a b ", "c", "a b c")]
    public void ApplyKeywordCompletion_ReplacesLastKeyword(string text, string keyword, string expected)
    {
        var actual = SearchPageViewModel.ApplyKeywordCompletion(text, keyword);
        Assert.AreEqual(expected, actual);
    }
}
