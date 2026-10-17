using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Filters;

namespace Pixeval.Tests;

[TestClass]
public sealed class FilterLanguageTest
{
    private static readonly FilterEngine _Engine = CreateLanguage();

    [ClassCleanup]
    public static void Cleanup() => _Engine.Dispose();

    [TestMethod]
    public void NumericTextAfterTagShouldRemainString()
    {
        var result = Analyze("#123");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Query);
        var ast = result.Query.GetAst();
        Assert.HasCount(1, ast.Children);
        var predicate = ast.Children.Single();
        Assert.AreEqual("Tag", predicate.SyntaxKey);
        Assert.AreEqual("123", predicate.TextContent);
    }

    [TestMethod]
    public void MissingTagValueShouldReturnTagCompletion()
    {
        var result = Analyze("#", 1);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(FilterDiagnosticKind.MissingTextValue, result.Diagnostics[0].Kind);
        Assert.AreEqual("#", result.Diagnostics[0].Arguments[0]);
        Assert.AreSequenceEqual((string[]) ["abc", "\"ab# c\""], result.Completions.Select(t => t.DisplayText).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.IsTrue(result.Completions.All(t => t.IsHintOnly));
    }

    [TestMethod]
    public void TagValueCompletionProviderShouldOverrideSyntaxExample()
    {
        var result = Analyze(
            "#to",
            3,
            new DelegateCallback(static context => context.MatchSyntaxKey == "Tag"
                ? [new("tag:touhou", "touhou", "touhou", "东方")]
                : []));

        Assert.Contains(t => t.DisplayText == "touhou", result.Completions);
        Assert.DoesNotContain(t => t.DisplayText == "#tag", result.Completions);
        var completion = result.Completions.Single(t => t.DisplayText == "touhou");
        Assert.AreEqual("touhou", completion.InsertText);
        Assert.AreEqual(FromBounds(1, 3), completion.ReplacementSpan);
    }

    [TestMethod]
    public void AuthorValueCompletionProviderShouldOverrideSyntaxExample()
    {
        var result = Analyze(
            "artist:sa",
            9,
            new DelegateCallback(static context => context.MatchSyntaxKey == "Author"
                ? [new("author:saberiii", "saberiii", "saberiii", null)]
                : []));

        Assert.Contains(t => t.DisplayText == "saberiii", result.Completions);
        Assert.DoesNotContain(t => t.DisplayText.Contains("artist", StringComparison.OrdinalIgnoreCase),
            result.Completions);
        var completion = result.Completions.Single(t => t.DisplayText == "saberiii");
        Assert.AreEqual(FromBounds(7, 9), completion.ReplacementSpan);
    }

    [TestMethod]
    public void MissingConstraintValueShouldReturnFlagCompletion()
    {
        var result = Analyze("+", 1);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreSequenceEqual((string[]) ["+ai", "+r18", "+r18g", "+gif"], result.Completions.Select(t => t.DisplayText).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void LongerFlagSyntaxShouldWinSharedPrefix()
    {
        var result = Analyze("+r18g");

        Assert.IsTrue(result.IsSuccess);
        var ast = result.Query!.GetAst();
        var predicate = ast.Children.Single();
        Assert.AreEqual("R18G", predicate.SyntaxKey);
    }

    [TestMethod]
    public void AliasCompletionShouldOnlyShowOneItemPerSyntax()
    {
        var result = Analyze(string.Empty, 0);

        Assert.IsTrue(result.IsSuccess);
        _ = Assert.ContainsSingle(t => t.DisplayText.Contains("artist", StringComparison.OrdinalIgnoreCase),
            result.Completions);
    }

    [TestMethod]
    public void EmptyInputShouldReturnIntrinsicAndRegisteredCompletions()
    {
        var result = Analyze(string.Empty, 0);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.IsSubsetOf((string[]) ["and", "or", "!"],
            result.Completions.Select(t => t.DisplayText).ToArray());
        Assert.Contains(t => t.DisplayText.Contains("artist", StringComparison.OrdinalIgnoreCase), result.Completions);
        var andCompletion = result.Completions.Single(t => t.DisplayText == "and");
        var orCompletion = result.Completions.Single(t => t.DisplayText == "or");
        Assert.AreEqual("(and", andCompletion.InsertText);
        Assert.AreEqual("(or", orCompletion.InsertText);
        Assert.AreEqual("逻辑与分组", andCompletion.Description);
    }

    [TestMethod]
    public void SyntaxExampleCompletionsShouldOnlyInsertHeader()
    {
        var result = Analyze(string.Empty, 0);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("", result.Completions.Single(t => t.DisplayText == "keyword").InsertText);
        Assert.AreEqual("a:", result.Completions.Single(t => t.DisplayText == "a:artist").InsertText);
        Assert.AreEqual("#", result.Completions.Single(t => t.DisplayText == "#tag").InsertText);
        Assert.AreEqual("l:", result.Completions.Single(t => t.DisplayText == "l:100-200").InsertText);
        Assert.AreEqual("s:", result.Completions.Single(t => t.DisplayText == "s:2024-1-1").InsertText);
        Assert.AreEqual("+ai", result.Completions.Single(t => t.DisplayText == "+ai").InsertText);
        Assert.AreEqual("-ai", result.Completions.Single(t => t.DisplayText == "-ai").InsertText);
    }

    [TestMethod]
    public void ValueSyntaxHintsShouldNotInsertText()
    {
        AssertValueHintsOnly("#", ["abc", "\"ab# c\""]);
        AssertValueHintsOnly("a:", ["abc", "\"ab# c\""]);
        AssertValueHintsOnly("score:", ["12345"]);
        AssertValueHintsOnly("weight:", ["2", "1.5", "1/2"]);
        AssertValueHintsOnly("l:", ["2-", "-3", "2-3"]);
        AssertValueHintsOnly("s:", ["MM-dd", "yyyy-MM-dd"]);
    }

    [TestMethod]
    public void ValueSyntaxHintsShouldIgnoreTypedPrefix()
    {
        var result = Analyze("s:202", 5);

        Assert.AreSequenceEqual((string[]) ["MM-dd", "yyyy-MM-dd"], result.Completions.Select(t => t.DisplayText).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.IsTrue(result.Completions.All(t => t.InsertText == "s:202"));
        Assert.IsTrue(result.Completions.All(t => t.IsHintOnly));
    }

    [TestMethod]
    public void ValueSyntaxHintsShouldSupportDoubleRange()
    {
        using var language = CreateRatioEngine();
        var result = language.Analyze("r:", 2, null);

        Assert.AreSequenceEqual((string[]) ["2-", "-1/2"], result.Completions.Select(t => t.DisplayText).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.IsTrue(result.Completions.All(t => t.InsertText == "r:"));
        Assert.IsTrue(result.Completions.All(t => t.IsHintOnly));
    }

    [TestMethod]
    public void LongValueSyntaxShouldBindLong()
    {
        var result = Analyze("score:12345");

        Assert.IsTrue(result.IsSuccess);
        var ast = result.Query!.GetAst();
        var predicate = ast.Children.Single();
        Assert.AreEqual("Score", predicate.SyntaxKey);
        Assert.AreEqual(12345L, predicate.LongValue);
    }

    [TestMethod]
    public void DoubleValueSyntaxShouldBindDouble()
    {
        var result = Analyze("weight:1/2");

        Assert.IsTrue(result.IsSuccess);
        var ast = result.Query!.GetAst();
        var predicate = ast.Children.Single();
        Assert.AreEqual("Weight", predicate.SyntaxKey);
        Assert.AreEqual(0.5d, predicate.DoubleValue, 0.000001d);
    }

    [TestMethod]
    public void EmptyInputShouldGroupFlagConstraintCompletions()
    {
        var result = Analyze(string.Empty, 0);
        var displayTexts = result.Completions.Select(t => t.DisplayText).ToArray();

        Assert.IsTrue(result.IsSuccess);
        Assert.Contains("+ai", displayTexts);
        Assert.Contains("-ai", displayTexts);
        CollectionAssert.DoesNotContain(displayTexts, "+r18");
        CollectionAssert.DoesNotContain(displayTexts, "+r18g");
        CollectionAssert.DoesNotContain(displayTexts, "+gif");
        CollectionAssert.DoesNotContain(displayTexts, "-r18");
        CollectionAssert.DoesNotContain(displayTexts, "-r18g");
        CollectionAssert.DoesNotContain(displayTexts, "-gif");
        Assert.AreEqual("正约束", result.Completions.Single(t => t.DisplayText == "+ai").Description);
        Assert.AreEqual("反约束", result.Completions.Single(t => t.DisplayText == "-ai").Description);
    }

    [TestMethod]
    public void PrefixConstraintInputShouldShowSpecificFlagCompletions()
    {
        var includeResult = Analyze("+", 1);
        var excludeResult = Analyze("-", 1);

        Assert.IsFalse(includeResult.IsSuccess);
        Assert.AreSequenceEqual((string[]) ["+ai", "+r18", "+r18g", "+gif"], includeResult.Completions.Select(t => t.DisplayText).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.IsFalse(excludeResult.IsSuccess);
        Assert.AreSequenceEqual((string[]) ["-ai", "-r18", "-r18g", "-gif"], excludeResult.Completions.Select(t => t.DisplayText).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void RightParenthesisShouldStillShowAllCompletions()
    {
        var result = Analyze("keyword)", 8);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(FilterDiagnosticKind.UnexpectedToken, result.Diagnostics[0].Kind);
        Assert.AreEqual(")", result.Diagnostics[0].Arguments[0]);
        CollectionAssert.IsSubsetOf((string[]) ["and", "or", "!"],
            result.Completions.Select(t => t.DisplayText).ToArray());
        Assert.Contains(t => t.DisplayText.Contains("artist", StringComparison.OrdinalIgnoreCase), result.Completions);
    }

    [TestMethod]
    public void NegationInputShouldShowUnprefixedDisplayText()
    {
        var result = Analyze("!", 1);

        Assert.IsFalse(result.IsSuccess);
        var authorCompletion =
            result.Completions.Single(t => t.DisplayText.Contains("artist", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(authorCompletion.DisplayText.StartsWith('!'));
        Assert.IsTrue(authorCompletion.InsertText.StartsWith('!'));
        CollectionAssert.IsSubsetOf((string[]) ["and", "or", "!"],
            result.Completions.Select(t => t.DisplayText).ToArray());
    }

    [TestMethod]
    public void LeftParenthesisShouldOnlyShowGroupOperatorCompletions()
    {
        var result = Analyze("(xxx", 4);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreSequenceEqual((string[]) ["and", "or"], result.Completions.Select(t => t.DisplayText).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.AreEqual("and", result.Completions.Single(t => t.DisplayText == "and").InsertText);
        Assert.AreEqual("or", result.Completions.Single(t => t.DisplayText == "or").InsertText);
    }

    [TestMethod]
    public void LeftParenthesisShouldNotEnterValueCompletion()
    {
        var result = Analyze(
            "(#",
            2,
            new DelegateCallback(static context => context.MatchSyntaxKey == "Tag"
                ? [new("tag:touhou", "touhou", "touhou", null)]
                : []));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreSequenceEqual((string[]) ["and", "or"], result.Completions.Select(t => t.DisplayText).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void BookmarkRangeShouldRemainInclusive()
    {
        var result = Analyze("l:100-200");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Query);
        var ast = result.Query.GetAst();
        var predicate = ast.Children.Single();
        Assert.AreEqual("Bookmark", predicate.SyntaxKey);
        Assert.AreEqual(100L, predicate.RangeStartLong);
        Assert.AreEqual(200L, predicate.RangeEndLong);
    }

    [TestMethod]
    public void MissingRangeDashShouldIncludeSyntaxAndLiteral()
    {
        var result = Analyze("l:100");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(FilterDiagnosticKind.InvalidLongRangeFormat, result.Diagnostics[0].Kind);
        Assert.AreEqual("l:", result.Diagnostics[0].Arguments[0]);
        Assert.AreEqual("100", result.Diagnostics[0].Arguments[1]);
    }

    [TestMethod]
    public void RangeOrderDiagnosticShouldIncludeSyntaxAndBounds()
    {
        var result = Analyze("l:200-100");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(FilterDiagnosticKind.RangeMinimumGreaterThanMaximum, result.Diagnostics[0].Kind);
        Assert.AreEqual("l:", result.Diagnostics[0].Arguments[0]);
        Assert.AreEqual("200", result.Diagnostics[0].Arguments[1]);
        Assert.AreEqual("100", result.Diagnostics[0].Arguments[2]);
    }

    [TestMethod]
    public void InvalidDateShouldIncludeSyntaxAndDateLiteral()
    {
        var result = Analyze("s:2024-2-31");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(FilterDiagnosticKind.InvalidDate, result.Diagnostics[0].Kind);
        Assert.AreEqual("s:", result.Diagnostics[0].Arguments[0]);
        Assert.AreEqual("2024-2-31", result.Diagnostics[0].Arguments[1]?.ToString());
    }

    private static FilterEngine CreateLanguage()
    {
        var syntaxes = new List<FilterSyntaxDefinition>
        {
            new("Title", FilterValueKind.Text, "keyword",
            [
                new("", [""], "", null, "keyword", null)
            ]),
            new("Author", FilterValueKind.Text, "artist",
            [
                new("", ["a"], ":", null, "artist", null),
                new("", ["artist"], ":", null, "artist", null)
            ]),
            new("Tag", FilterValueKind.Text, "tag",
            [
                new("#", [""], "", null, "tag", null)
            ]),
            new("Bookmark", FilterValueKind.LongRange, "100-200",
            [
                new("", ["l"], ":", null, "100-200", null)
            ]),
            new("Score", FilterValueKind.Long, "12345",
            [
                new("", ["score"], ":", null, "12345", null)
            ]),
            new("Weight", FilterValueKind.Double, "1/2",
            [
                new("", ["weight"], ":", null, "1/2", null)
            ]),
            new("StartDate", FilterValueKind.Date, "2024-1-1",
            [
                new("", ["s"], ":", null, "2024-1-1", null)
            ]),
            new("Ai", FilterValueKind.Flag, null,
            [
                new("+", ["ai"], "", "false", null, "仅显示 AI"),
                new("-", ["ai"], "", "true", null, "排除 AI")
            ]),
            new("R18", FilterValueKind.Flag, null,
            [
                new("+", ["r18"], "", "false", null, "仅显示 R18"),
                new("-", ["r18"], "", "true", null, "排除 R18")
            ]),
            new("R18G", FilterValueKind.Flag, null,
            [
                new("+", ["r18g"], "", "false", null, "仅显示 R18G"),
                new("-", ["r18g"], "", "true", null, "排除 R18G")
            ]),
            new("Gif", FilterValueKind.Flag, null,
            [
                new("+", ["gif"], "", "false", null, "仅显示动图"),
                new("-", ["gif"], "", "true", null, "排除动图")
            ])
        };

        List<FilterCompletionDefinition> intrinsic =
        [
            new("builtin.and", "and", "and", "逻辑与分组"),
            new("builtin.or", "or", "or", "逻辑或分组"),
            new("builtin.not", "!", "!", "逻辑非")
        ];

        List<FilterFullCompletionDefinition> full =
        [
            new("constraint.include", "+ai", "+ai", "正约束", ["+"]),
            new("constraint.exclude", "-ai", "-ai", "反约束", ["-"])
        ];

        List<FilterValueHintGroup> hints =
        [
            new(FilterValueKind.Text,
            [
                new("hint.text.plain", "abc", "", "普通的字符串"),
                new("hint.text.quoted", "\"ab# c\"", "", "带空格或转义字符的字符串")
            ]),
            new(FilterValueKind.Long,
            [
                new("hint.long.plain", "12345", "", "普通的整数")
            ]),
            new(FilterValueKind.Double,
            [
                new("hint.double.integer", "2", "", "整数形式的小数值"),
                new("hint.double.decimal", "1.5", "", "小数形式的小数值"),
                new("hint.double.fraction", "1/2", "", "分数形式的小数值")
            ]),
            new(FilterValueKind.LongRange,
            [
                new("hint.long-range.lower", "2-", "", "大于等于 2"),
                new("hint.long-range.upper", "-3", "", "小于等于 3"),
                new("hint.long-range.closed", "2-3", "", "大于等于 2 且小于等于 3")
            ]),
            new(FilterValueKind.DoubleRange,
            [
                new("hint.double-range.lower", "2-", "", "大于等于 2"),
                new("hint.double-range.upper-fraction", "-1/2", "", "小于等于 1/2")
            ]),
            new(FilterValueKind.Date,
            [
                new("hint.date.month-day-dash", "MM-dd", "", "今年某月某日"),
                new("hint.date.full-dash", "yyyy-MM-dd", "", "某年某月某日")
            ])
        ];

        return new FilterEngine(syntaxes, intrinsic, full, hints);
    }

    private static FilterEngine CreateRatioEngine()
    {
        var syntaxes = new List<FilterSyntaxDefinition>
        {
            new("Ratio", FilterValueKind.DoubleRange, "1-2",
            [
                new("", ["r"], ":", null, "1-2", null)
            ])
        };

        List<FilterValueHintGroup> hints =
        [
            new(FilterValueKind.DoubleRange,
            [
                new("hint.double-range.lower", "2-", "", null),
                new("hint.double-range.upper-fraction", "-1/2", "", null)
            ])
        ];

        return new FilterEngine(syntaxes, null, null, hints);
    }

    private static FilterAnalysisResult Analyze(string text) => _Engine.Analyze(text, text.Length, null);

    private static FilterAnalysisResult Analyze(string text, int caretPosition, IFilterCompletionCallback? callback = null) =>
        _Engine.Analyze(text, caretPosition, callback);

    private static void AssertValueHintsOnly(string text, IReadOnlyCollection<string> displayTexts)
    {
        var result = Analyze(text, text.Length);

        Assert.AreSequenceEqual(displayTexts.ToArray(), result.Completions.Select(t => t.DisplayText).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.IsTrue(result.Completions.All(t => t.InsertText == text));
        Assert.IsTrue(result.Completions.All(t => t.IsHintOnly));
    }

    private static FilterTextSpan FromBounds(int start, int end) => new(Math.Max(start, 0), Math.Max(0, end - start));

    private sealed class DelegateCallback(Func<FilterValueCompletionContext, FilterCompletionDefinition[]> provider)
        : IFilterCompletionCallback
    {
        public List<FilterCompletionDefinition> GetCompletions(FilterValueCompletionContext context) =>
            [.. provider(context)];
    }
}
