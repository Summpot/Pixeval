using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MaterialCompatibilityTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MixedThemeTypographyStillNeedsItsCompatibilityBoundary(bool compatibility)
    {
        await using var session = HeadlessUnitTestSession.StartNew(compatibility
            ? typeof(MaterialThemeIntegrationTest.MaterialTestApplication)
            : typeof(WithoutCompatibilityApplication));
        await session.Dispatch(() =>
        {
            var fallback = new TextBlock { FontSize = 32, Text = "字体 Font\n第二行" };
            var item = new TabStripItem { Content = "标签 Tab" };
            var tabs = new TabStrip { ItemsSource = new[] { item }, SelectedIndex = 0 };
            var window = new Window { Width = 480, Height = 320, Content = new StackPanel { Children = { fallback, tabs } } };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    Application.Current!.RequestedThemeVariant = theme;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    if (compatibility)
                    {
                        Assert.IsTrue(double.IsNaN(fallback.LineHeight), "Fallback text must retain automatic line height at a larger font size.");
                        Assert.IsTrue(fallback.Bounds.Height >= 64, "Two 32-DIP lines must not be compressed into the body line height.");
                        Assert.AreEqual(14d, item.FontSize);
                        Assert.AreEqual(20d, item.GetValue(TextBlock.LineHeightProperty));
                    }
                    else
                    {
                        Assert.AreEqual(24d, fallback.LineHeight, "Revisit the fallback rule if upstream stops imposing BodyLarge line height.");
                        Assert.AreEqual(24d, item.GetValue(TextBlock.LineHeightProperty), "Revisit the tab rule if upstream supplies a complete item typography.");
                    }
                }
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    public sealed class WithoutCompatibilityApplication : Application
    {
        public override void Initialize()
        {
            var theme = (Styles) AvaloniaXamlLoader.Load(new Uri("avares://Pixeval/Themes/PixevalTheme.axaml"));
            // Compiled StyleInclude entries are embedded Styles, not runtime include objects.
            var compatibility = theme.OfType<Styles>().Single(styles => styles.OfType<Style>().Any(style =>
                style.Selector?.ToString() is { } selector && selector.Contains("ColorPicker /template/ DropDownButton", StringComparison.Ordinal)));
            _ = theme.Remove(compatibility);
            Styles.Add(theme);
        }

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<WithoutCompatibilityApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false, ShouldRenderOnUIThread = true })
            .WithPixevalFonts();
    }
}
