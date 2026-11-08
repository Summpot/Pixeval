using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Utilities;
using Pixeval.Common;

namespace Pixeval.Tests;

[TestClass]
public sealed class FontFamilyHelperTest
{
    [TestMethod]
    public void NormalFontFamily_IsUsable()
    {
        Assert.IsTrue(FontFamilyHelper.IsUsable(new FontFamily("Anita Semi-squre")));
    }

    [TestMethod]
    [DataRow("Anita  Semi-squre")]
    [DataRow("Noto   Sans SC")]
    public void RepeatedWhitespaceFontFamily_RemainsUsable(string name)
    {
        // Avalonia 12.1.5 skips repeated whitespace when normalizing typeface names.
        // Such names must remain available instead of being discarded as invalid.
        Assert.IsTrue(FontFamilyHelper.IsUsable(new FontFamily(name)));
        Assert.AreEqual(name, FontFamilyHelper.Create(name)?.Name);
    }

    [TestMethod]
    public void EmptyFontFamily_IsRemovedFromFallbackList()
    {
        var fontFamily = FontFamilyHelper.Create(["", "  ", "Arial"]);

        Assert.IsNotNull(fontFamily);
        Assert.AreEqual("Arial", fontFamily.Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t ")]
    public void EmptyFontSetting_UsesTheDefaultFont(string? name) =>
        Assert.IsNull(FontFamilyHelper.Create(name));

    [TestMethod]
    [DoNotParallelize]
    public async Task RepeatedWhitespaceFontSetting_RendersWithCjkFallback()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            App.ApplyAppFontFamily(["Anita  Semi-squre", "fonts:PixevalCjk#Noto Sans SC"]);
            var text = new TextBlock { Text = "中文字体 Font fallback", FontSize = 24 };
            var window = new Window { Width = 480, Height = 160, Content = text };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                using var frame = window.CaptureRenderedFrame();
                Assert.IsNotNull(frame);
                Assert.IsTrue(text.DesiredSize.Width > 0 && text.DesiredSize.Height >= 24);
                Assert.AreEqual("Anita  Semi-squre", text.FontFamily.Name);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }
}
