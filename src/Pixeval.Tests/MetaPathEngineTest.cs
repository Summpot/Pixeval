// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Download;
using Pixeval.Utilities;
using Pixeval.Common;

namespace Pixeval.Tests;

[TestClass]
public sealed class MetaPathEngineTest
{
    [TestMethod]
    public void ValidMacroShouldProduceHighlightsAndSuccess()
    {
        var result = PixevalDownloadMethods.AnalyzeMetaPath("@{id}");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.Diagnostics.Count);
        Assert.AreEqual(4, result.Highlights.Count);
        Assert.IsTrue(result.Highlights.Any(h => h.Kind == MacroHighlightKind.Name));
    }

    [TestMethod]
    public void FormattedTransducerShouldProduceFormatterHighlight()
    {
        var result = PixevalDownloadMethods.AnalyzeMetaPath("@{ext:u}");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.Diagnostics.Count);
        Assert.AreEqual(6, result.Highlights.Count);
        Assert.IsTrue(result.Highlights.Any(h => h.Kind == MacroHighlightKind.Formatter));
    }

    [TestMethod]
    public void MissingRightBraceShouldProduceDiagnostic()
    {
        var result = PixevalDownloadMethods.AnalyzeMetaPath("@{id");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Diagnostics.Count > 0);
        Assert.AreEqual(MacroDiagnosticKind.MissingRightBrace, result.Diagnostics[0].Kind);
    }

    [TestMethod]
    public void UnknownMacroShouldProduceDiagnostic()
    {
        var result = PixevalDownloadMethods.AnalyzeMetaPath("@{non_existent_macro}");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(MacroDiagnosticKind.UnknownMacroName, result.Diagnostics[0].Kind);
        Assert.AreEqual("non_existent_macro", result.Diagnostics[0].Arguments[0]);
    }

    [TestMethod]
    public void PredicateWithoutBranchesShouldProduceDiagnostic()
    {
        var result = PixevalDownloadMethods.AnalyzeMetaPath("@{is_pic_set}");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(MacroDiagnosticKind.ConditionalBranchesMissing, result.Diagnostics[0].Kind);
    }

    [TestMethod]
    public void ReduceShouldFormatIdAndDateTime()
    {
        var sample = DesignHelper.DownloadParserSampleSingleIllustration;
        var context = new ParserContext(sample).ToMacroContext();

        var idPath = PixevalDownloadMethods.ReduceMetaPath("@{id}", context);
        Assert.AreEqual("123456", idPath);

        var datePath = PixevalDownloadMethods.ReduceMetaPath("@{publish_time:yyyy-MM-dd}", context);
        Assert.AreEqual(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"), datePath);

        var extPath = PixevalDownloadMethods.ReduceMetaPath("@{ext}", context);
        Assert.AreEqual("<ext>", extPath);

        var formattedExtPath = PixevalDownloadMethods.ReduceMetaPath("@{ext:u}", context);
        Assert.AreEqual("<ext:u>", formattedExtPath);
    }

    [TestMethod]
    public void ReduceShouldHandleConditionals()
    {
        var singleSample = DesignHelper.DownloadParserSampleSingleIllustration;
        var singleContext = new ParserContext(singleSample).ToMacroContext();
        var singleResult = PixevalDownloadMethods.ReduceMetaPath("@{is_pic_set?set:single}", singleContext);
        Assert.AreEqual("single", singleResult);

        var setSample = DesignHelper.DownloadParserSampleImageSetIllustration;
        var setContext = new ParserContext(setSample).ToMacroContext();
        var setResult = PixevalDownloadMethods.ReduceMetaPath("@{is_pic_set?set:single}", setContext);
        Assert.AreEqual("set", setResult);
    }
}
