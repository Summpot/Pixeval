using System;
using System.Globalization;
using System.IO;
using Avalonia.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.Views.Converters;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DateTimeOffsetTextConverterTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        I18NManager.CandidatePaths.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval")));
        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
    }

    [TestMethod]
    [DataRow("2026-11-07T13:24:35.1234567+05:30")]
    [DataRow("2024-02-29T23:59:59.9999999-03:30")]
    [DataRow("0001-01-01T00:00:00.0000000+00:00")]
    [DataRow("9999-12-31T23:59:59.9999999+00:00")]
    [DataRow("2026-11-07T13:24:35+14:00")]
    [DataRow("2026-11-07T13:24:35-14:00")]
    public void RoundTripPreservesAllComponentsIndependentlyOfUiCulture(string text)
    {
        var expected = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
        var converter = DateTimeOffsetTextConverter.Instance;
        var parsed = Assert.IsInstanceOfType<DateTimeOffset>(converter.ConvertBack(text, typeof(DateTimeOffset), null, CultureInfo.GetCultureInfo("fr-FR")));
        Assert.IsTrue(expected.EqualsExact(parsed));
        var formatted = converter.Convert(parsed, typeof(string), null, CultureInfo.GetCultureInfo("ar-SA"));
        Assert.AreEqual(expected.ToString("O", CultureInfo.InvariantCulture), formatted);
        var restored = Assert.IsInstanceOfType<DateTimeOffset>(converter.ConvertBack(formatted, typeof(DateTimeOffset), null, CultureInfo.GetCultureInfo("zh-CN")));
        Assert.IsTrue(expected.EqualsExact(restored));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("2026-11-07")]
    [DataRow("2026-11-07T13:24:35")]
    [DataRow("2026-02-29T13:24:35+08:00")]
    [DataRow("2026-11-07T13:24:35+14:01")]
    [DataRow("2026-11-07T13:24:35.12345678+08:00")]
    [DataRow("0001-01-01T00:00:00+14:00")]
    [DataRow("9999-12-31T23:59:59-14:00")]
    public void InvalidOrIncompleteInputReturnsValidationErrorWithoutAReplacementValue(string? text)
    {
        var result = Assert.IsInstanceOfType<BindingNotification>(DateTimeOffsetTextConverter.Instance.ConvertBack(
            text, typeof(DateTimeOffset), null, CultureInfo.InvariantCulture));
        Assert.AreEqual(BindingErrorType.DataValidationError, result.ErrorType);
        Assert.IsFalse(result.HasValue);
        Assert.AreEqual(I18NManager.GetResource(SettingsMainViewResources.InvalidDateTimeOffset), result.Error?.Message);
    }
}
