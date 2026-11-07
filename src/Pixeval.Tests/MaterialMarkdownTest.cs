using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MaterialMarkdownTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        I18NManager.CandidatePaths.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval")));
        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task CodeCopyPreservesTextKeyboardAndIndependentScroll(bool dark, bool indented)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            const string code = "中文🌸 <tag> \"quoted\"\n\tsecond line " + "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz";
            var markdown = new MarkdownBox
            {
                Markdown = (indented ? "    " + code.Replace("\n", "\n    ") + "\n" : "```text\n" + code + "\n```\n")
                    + "\n" + string.Join("\n\n", Enumerable.Repeat("Document paragraph / 文档段落", 40))
            };
            var window = new Window { Width = 360, Height = 520, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = markdown };
            try
            {
                window.Show();
                Render(window);
                var copy = markdown.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("CopyButton"));
                var scrollers = markdown.GetVisualDescendants().OfType<ScrollViewer>().ToArray();
                var codeScroll = scrollers.Single(viewer => viewer.Classes.Contains("CodeBlock"));
                var document = scrollers.First();
                foreach (var viewer in scrollers)
                    viewer.IsScrollInertiaEnabled = false;
                Assert.IsTrue(copy.Bounds.Width >= 48 && copy.Bounds.Height >= 48, "Code copy needs a usable target.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(copy)));
                Assert.IsTrue(copy.TranslatePoint(new(0, copy.Bounds.Height), markdown)!.Value.Y <= codeScroll.TranslatePoint(default, markdown)!.Value.Y,
                    "The code toolbar must not cover the first code line.");
                Assert.IsTrue(copy.Focus());
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Render(window);
                var clipboard = window.Clipboard;
                Assert.IsNotNull(clipboard);
                // Markdown.Avalonia expands tabs before invoking the code-block parser.
                Assert.AreEqual(code.Replace("\t", "    "), await clipboard.TryGetTextAsync());
                if (Environment.GetEnvironmentVariable("PIXEVAL_MARKDOWN_CAPTURE") is { Length: > 0 } directory)
                {
                    _ = Directory.CreateDirectory(directory);
                    using var frame = window.CaptureRenderedFrame();
                    Assert.IsNotNull(frame);
                    frame.Save(Path.Combine(directory, $"code-{dark}-{indented}.png"), new PngBitmapEncoderOptions());
                }

                Assert.IsTrue(codeScroll.Extent.Width > codeScroll.Viewport.Width);
                var point = codeScroll.TranslatePoint(new(20, 10), window)!.Value;
                window.MouseWheel(point, new(-2, 0));
                Render(window);
                Assert.IsTrue(codeScroll.Offset.X > 0);
                Assert.AreEqual(0, document.Offset.Y);
                // The existing horizontal-only presenter maps the wheel to X until
                // it reaches its edge; only then does it chain to the document.
                codeScroll.Offset = new(codeScroll.Extent.Width - codeScroll.Viewport.Width, 0);
                Render(window);
                window.MouseWheel(point, new(0, -2));
                Render(window);
                Assert.IsTrue(document.Offset.Y > 0, "Vertical wheel must continue to the document.");

                markdown.Markdown = "```text\nreplacement\n```";
                Render(window);
                copy = markdown.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("CopyButton"));
                Assert.IsTrue(copy.Focus());
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                Render(window);
                Assert.AreEqual("replacement", await clipboard.TryGetTextAsync(), "Rebuilt toolbars must copy the current code.");
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InlineCodeUsesTheApplicationFontFallbackAndResourceChanges(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(() =>
        {
            var markdown = new MarkdownBox { Markdown = "Before `inline 中文 🌸` after." };
            var window = new Window { Width = 360, Height = 240, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = markdown };
            try
            {
                window.Show();
                Render(window);
                var code = markdown.GetVisualDescendants().OfType<CTextBlock>().SelectMany(block => block.Content).OfType<CCode>().Single();
                Assert.AreEqual(markdown.FindResource("PixevalMonospaceFontFamily"), code.MonospaceFontFamily, "Inline code must not depend on a Windows-only font picked by the package.");
                Assert.IsNotNull(new Typeface(code.FontFamily).GlyphTypeface);
                markdown.Resources["PixevalMonospaceFontFamily"] = FontFamily.Default;
                Render(window);
                Assert.AreEqual(FontFamily.Default, code.MonospaceFontFamily, "A local font resource override must update existing inline code.");
                Assert.IsNotNull(new Typeface(code.FontFamily).GlyphTypeface);
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    private static void Render(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
    }
}
