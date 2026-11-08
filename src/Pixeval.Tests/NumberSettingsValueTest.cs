using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoSettingsPage.Avalonia;
using AutoSettingsPage.Models;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class NumberSettingsValueTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {

        AppLocalization.Instance.SetLanguage("zh-Hans");
        LocalSettingsEntryHelper.Initialize();
    }

    [TestMethod]
    [DataRow(false, "en-US")]
    [DataRow(true, "fr-FR")]
    public async Task GeneratedNumberEditorsPreserveFractionsAndIntegerSemantics(bool dark, string cultureName)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var settings = new SampleSettings();
            var entry = new NumberSettingsEntry<SampleSettings, double>(settings, "fraction", "Fraction", "Fractional setting", default, null,
                value => value.Fraction, (value, number) => value.Fraction = number) { Min = 0.25, Max = 4, Step = 0.25 };
            var integer = new NumberSettingsEntry<SampleSettings, int>(settings, "integer", "Integer", "Integer setting", default, null,
                value => value.Integer, (value, number) => value.Integer = number) { Min = 1, Max = 10, Step = 1 };
            var control = SettingsEntryHelper.GetControl(entry);
            var integerControl = SettingsEntryHelper.GetControl(integer);
            var window = new Window
            {
                Width = 800, Height = 400, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new StackPanel { Children = { control, integerControl } }
            };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                window.UpdateLayout();
                var numeric = control.GetVisualDescendants().OfType<NumericUpDown>().Single();
                var wholeNumber = integerControl.GetVisualDescendants().OfType<NumericUpDown>().Single();
                var culture = CultureInfo.GetCultureInfo(cultureName);
                numeric.NumberFormat = culture.NumberFormat;
                Assert.AreEqual(1.5m, numeric.Value);
                Assert.AreEqual(1.5m.ToString(culture), numeric.Text, "The editor must display the actual fractional value.");
                Assert.AreEqual(0.25m, numeric.Minimum);
                Assert.AreEqual(4m, numeric.Maximum);
                Assert.AreEqual(0.25m, numeric.Increment);
                Assert.AreEqual("3", wholeNumber.Text);
                Assert.AreEqual("F0", wholeNumber.FormatString);
                var textBox = numeric.GetVisualDescendants().OfType<TextBox>().Single();
                Assert.IsTrue(textBox.Focus());
                textBox.SelectAll();
                window.KeyTextInput(2.75m.ToString(culture));
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Assert.AreEqual(2.75, entry.Value);
                Assert.AreEqual(2.75, settings.Fraction);
                window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
                window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
                Assert.AreEqual(3, entry.Value);
                entry.Value = 0.5;
                Assert.AreEqual(0.5m.ToString(culture), numeric.Text);
                Assert.AreEqual(3, settings.Integer);
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        }, default);
    }

    private sealed class SampleSettings
    {
        public double Fraction { get; set; } = 1.5;
        public int Integer { get; set; } = 3;
    }
}
