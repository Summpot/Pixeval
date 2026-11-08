using System;
using System.Collections.Generic;
using FluentIcons.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Extensions.Common.Settings;
using Pixeval.Services.Background;

namespace Pixeval.Tests;

[TestClass]
public sealed class ExtensionDateSettingTest
{
    [TestMethod]
    public void ChangingOffsetForTheSameInstantUpdatesAndPersistsTheExactValue()
    {
        var original = new DateTimeOffset(2026, 11, 7, 13, 24, 35, TimeSpan.FromMinutes(330)).AddTicks(1234567);
        var changed = original.ToOffset(TimeSpan.FromMinutes(-210));
        var calls = 0;
        var received = default(DateTimeOffset);
        var entry = CreateEntry(original, value => { calls++; received = value; });

        entry.Value = changed;

        Assert.IsTrue(entry.Value.EqualsExact(changed), "An offset-only edit must not be discarded as an equal UTC instant.");
        Assert.IsTrue(received.EqualsExact(changed));
        Assert.AreEqual(1, calls);
        entry.Value = changed;
        Assert.AreEqual(1, calls, "Assigning the exact same value does not repeat the native callback.");
        var values = new Dictionary<string, object?>();
        entry.ValueSaving(values);
        Assert.IsTrue(Assert.IsInstanceOfType<DateTimeOffset>(values["date"]).EqualsExact(changed));
        Assert.AreEqual(2, calls, "Saving forwards the exact value to the extension.");
        entry.ValueReset();
        Assert.IsTrue(entry.Value.EqualsExact(original));
        Assert.AreEqual(3, calls);
    }

    internal static ExtensionSettingsEntry<ISettingsExtension, DateTimeOffset> CreateEntry(DateTimeOffset initial, Action<DateTimeOffset>? changed = null) =>
        new(new SettingMetadata(), initial, _ => initial, changed ?? (_ => { }));

    private sealed class SettingMetadata : ISettingsExtension
    {
        public Symbol GetIcon() => default;
        public string GetLabel() => "Fixture date";
        public string GetDescription() => "Date with explicit offset";
        public string GetToken() => "date";
        public string? GetDescriptionUri() => null;
        public string? GetPlaceholder() => null;
        public SettingsType GetSettingsType() => SettingsType.DateTimeOffset;
        public void OnExtensionLoaded() { }
        public void OnExtensionUnloaded() { }
    }
}
