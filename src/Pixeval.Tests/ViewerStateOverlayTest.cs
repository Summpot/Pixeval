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
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ViewerStateOverlayTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        I18NManager.CandidatePaths.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval")));
        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
    }

    [TestMethod]
    [DataRow(false, 1000)]
    [DataRow(true, 1000)]
    [DataRow(false, 320)]
    [DataRow(true, 320)]
    public async Task LoadingErrorAndRecoveryPreserveInputAndFullErrorText(bool dark, int width)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            var underlying = new Button { Content = "Viewer action", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            var overlay = new ViewerStateOverlay();
            var window = new Window
            {
                Width = width,
                Height = 480,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new Grid { Children = { underlying, overlay } }
            };
            try
            {
                window.Show();
                await RenderAsync(window);
                var clicks = 0;
                underlying.Click += (_, _) => clicks++;
                Click(window);
                Assert.AreEqual(1, clicks, "An idle overlay must not intercept viewer input.");
                overlay.IsLoading = true;
                await RenderAsync(window);
                var progress = overlay.GetVisualDescendants().OfType<ProgressBar>().Single();
                Assert.IsTrue(progress.IsEffectivelyVisible && progress.IsIndeterminate);
                Click(window);
                Assert.AreEqual(1, clicks, "Loading must still cover the underlying viewer.");

                var message = string.Join("\n", Enumerable.Repeat("完整错误详情 / Full error text, including the final diagnostic line.", 60)) + "\nFINAL 🌸";
                overlay.IsLoading = false;
                overlay.ErrorMessage = message;
                await RenderAsync(window);
                Assert.IsFalse(progress.IsEffectivelyVisible || progress.IsIndeterminate);
                var error = overlay.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == message);
                var scroll = error.GetVisualAncestors().OfType<ScrollViewer>().SingleOrDefault();
                Assert.IsNotNull(scroll, "Errors longer than the viewport must remain readable through scrolling.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(progress)), "Loading progress needs its localized accessible name.");
                Assert.IsTrue(scroll.Viewport.Height > 0 && scroll.Extent.Height > scroll.Viewport.Height);
                Assert.IsTrue(error.TextLayout.Width <= error.Bounds.Width + 1);
                var position = scroll.TranslatePoint(default, window)!.Value;
                Assert.IsTrue(position.X >= 24 && position.Y >= 24);
                Assert.IsTrue(position.X + scroll.Bounds.Width <= width - 24 + 1 && position.Y + scroll.Bounds.Height <= window.Height - 24 + 1);
                Assert.IsTrue(underlying.Focus());
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
                window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
                Assert.AreSame(scroll, window.FocusManager.GetFocusedElement(), "Long errors must be reachable through keyboard navigation.");
                window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
                window.KeyRelease(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
                await RenderAsync(window);
                Assert.IsTrue(scroll.Offset.Y > 0, "PageDown must advance through the error details.");
                scroll.Offset = new(0, scroll.Extent.Height - scroll.Viewport.Height);
                await RenderAsync(window);
                Assert.IsTrue(scroll.Offset.Y > 0);
                Assert.AreEqual(message, error.Text);
                Click(window);
                Assert.AreEqual(1, clicks, "An error must not pass pointer input to the covered viewer.");
                if (Environment.GetEnvironmentVariable("PIXEVAL_OVERLAY_CAPTURE") is { Length: > 0 } directory)
                {
                    _ = Directory.CreateDirectory(directory);
                    using var frame = window.CaptureRenderedFrame();
                    Assert.IsNotNull(frame);
                    frame.Save(Path.Combine(directory, $"viewer-error-{dark}-{width}.png"), new PngBitmapEncoderOptions());
                }
                overlay.ErrorMessage = null;
                await RenderAsync(window);
                Click(window);
                Assert.AreEqual(2, clicks, "Clearing the state must restore viewer input.");
                overlay.IsLoading = true;
                await RenderAsync(window);
                var panel = (Grid) window.Content!;
                _ = panel.Children.Remove(overlay);
                await RenderAsync(window);
                Assert.IsFalse(overlay.IsLoaded);
                panel.Children.Add(overlay);
                await RenderAsync(window);
                Assert.IsTrue(overlay.IsLoaded && progress.IsIndeterminate && progress.IsEffectivelyVisible);
                overlay.IsLoading = false;
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    private static void Click(Window window)
    {
        var point = new Point(window.Width / 2, window.Height / 2);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(100);
    }
}
