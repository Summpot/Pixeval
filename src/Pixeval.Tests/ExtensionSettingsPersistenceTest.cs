using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Services.Core;
using Pixeval.Config;
using Pixeval.Services.Background;
using System.Text.Json;

namespace Pixeval.Tests;

[TestClass]
public sealed class ExtensionSettingsPersistenceTest
{
    [TestMethod]
    public void IntegerAndEnumValuesSurviveNativeYaml()
    {
        var values = RoundTrip(7);
        Assert.AreEqual(7, values.TryGetTargetOrAddDefault("value", 3));
    }

    [TestMethod]
    public void ColorRetainsAllBitsAcrossNativeYaml()
    {
        var values = RoundTrip(0x80224466u);
        Assert.AreEqual(0x80224466u, values.TryGetTargetOrAddDefault("value", 0u));
    }

    [TestMethod]
    public void DateRetainsTimeAndOffsetAcrossNativeYaml()
    {
        var date = new DateTimeOffset(2026, 11, 7, 13, 24, 35, TimeSpan.FromMinutes(330)).AddTicks(1234);
        var values = RoundTrip(date);
        Assert.IsTrue(date.EqualsExact(values.TryGetTargetOrAddDefault("value", DateTimeOffset.MinValue)));
    }

    [TestMethod]
    public void StringCollectionRetainsOrderAndUnicodeAcrossNativeYaml()
    {
        var values = RoundTrip(new ObservableCollection<string> { "fixture 中文", "", "second" });
        CollectionAssert.AreEqual(new[] { "fixture 中文", "", "second" },
            values.TryGetTargetOrAddDefault("value", Array.Empty<string>()));
    }

    [TestMethod]
    public void WholeDoubleDoesNotBecomeTheDefaultAfterReload()
    {
        var values = RoundTrip(2d);
        Assert.AreEqual(2d, values.TryGetTargetOrAddDefault("value", 1.5d));
    }

    [TestMethod]
    public void ConversionRejectsOverflowFractionalIntegersAndMixedCollections()
    {
        Dictionary<string, object?> values = new()
        {
            ["negative"] = -1L, ["overflow"] = (long) uint.MaxValue + 1,
            ["fraction"] = 1.5d, ["mixed"] = new object[] { "valid", 42 },
            ["precision"] = 9007199254740993L, ["maximum"] = long.MaxValue
        };
        Assert.IsFalse(values.TryGetTarget<uint>("negative", out _));
        Assert.IsFalse(values.TryGetTarget<uint>("overflow", out _));
        Assert.IsFalse(values.TryGetTarget<int>("fraction", out _));
        Assert.IsFalse(values.TryGetTarget<string[]>("mixed", out _));
        Assert.IsFalse(values.TryGetTarget<double>("precision", out _));
        Assert.IsFalse(values.TryGetTarget<double>("maximum", out _));
        Assert.AreEqual(-1L, values["negative"], "A failed read must not overwrite the stored value.");
    }

    private static Dictionary<string, object?> RoundTrip(object value)
    {
        var settings = new AppSettings { ExtensionSettings = new() { ["fixture"] = new() { ["value"] = value } } };
        var json = JsonSerializer.Serialize(settings, SettingsSerializerContext.Default.AppSettings);
        var yaml = PixevalConfigMethods.JsonToYaml(json);
        var restored = PixevalConfigMethods.YamlToJson(yaml);
        return JsonSerializer.Deserialize(restored, SettingsSerializerContext.Default.AppSettings)!.ExtensionSettings["fixture"];
    }
}
