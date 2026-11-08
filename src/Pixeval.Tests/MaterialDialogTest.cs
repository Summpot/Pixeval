using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Material3Expressive.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MaterialDialogTest
{
    [TestMethod]
    [DataRow(false, 900)]
    [DataRow(true, 900)]
    [DataRow(false, 360)]
    [DataRow(true, 360)]
    public async Task LongContentAndActionsFitAndRemainOperable(bool dark, int width)
    {
        await RunAsync(width, dark, async (window, host, origin) =>
        {
            var dialog = new ContentDialog
            {
                Title = "完整的对话框标题 / A long dialog title 🌸",
                Content = string.Join("\n", Enumerable.Repeat("保留完整说明，正文滚动不遮挡操作按钮。Long text remains scrollable.", 40)),
                PrimaryButtonText = "保存并继续当前操作 / Save and continue",
                SecondaryButtonText = "保留草稿稍后处理 / Keep draft for later",
                CloseButtonText = "取消并返回原页面 / Cancel and return",
                DefaultButton = ContentDialogButton.Primary
            };
            var shown = dialog.ShowAsync(host);
            await SettleAsync(window);
            Capture(window, $"dialog-{width}-{dark}");
            var buttons = dialog.GetVisualDescendants().OfType<Button>().Where(button => button.Name is "PART_PrimaryButton" or "PART_SecondaryButton" or "PART_CloseButton").ToArray();
            Assert.HasCount(3, buttons);
            foreach (var button in buttons)
            {
                var position = button.TranslatePoint(default, window)!.Value;
                Assert.IsTrue(position.X >= 0 && position.X + button.Bounds.Width <= width + 1, $"{button.Name} is clipped horizontally: {position}/{button.Bounds}.");
                Assert.IsTrue(position.Y >= 0 && position.Y + button.Bounds.Height <= window.Height + 1, $"{button.Name} is clipped vertically.");
                Assert.IsTrue(button.Bounds.Height >= 48);
                var text = button.GetVisualDescendants().OfType<TextBlock>().Single();
                Assert.IsTrue(text.TextLayout.Width <= text.Bounds.Width + 1 && text.TextLayout.Height <= text.Bounds.Height + 1, "A complete action label must remain readable.");
            }
            var surface = dialog.GetVisualDescendants().OfType<MaterialSurface>().SingleOrDefault(control => control.Name is "PART_LayoutRoot");
            Assert.IsNotNull(surface, "The dialog must use the Material surface while retaining its behavior host.");
            if (width < 560)
                Assert.AreEqual(window.Height, surface.TranslatePoint(new(0, surface.Bounds.Height), window)!.Value.Y, 1, "Compact dialogs must reach the intended bottom-sheet edge.");
            var body = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.IsTrue(body.Extent.Height > body.Viewport.Height && body.Viewport.Height > 0);
            body.Offset = new(0, body.Extent.Height - body.Viewport.Height);
            await SettleAsync(window);
            Assert.IsTrue(body.Offset.Y > 0);

            var primaryCalls = 0;
            ContentDialogDeferral? pending = null;
            dialog.PrimaryButtonClick += (_, args) => { primaryCalls++; pending = args.GetDeferral(); };
            var primary = buttons.Single(button => button.Name is "PART_PrimaryButton");
            Click(window, primary);
            Click(window, primary);
            Assert.AreEqual(1, primaryCalls);
            Assert.IsFalse(shown.IsCompleted);
            Assert.IsNotNull(pending);
            pending.Complete();
            Assert.AreEqual(ContentDialogResult.Primary, await shown.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.AreSame(origin, window.FocusManager.GetFocusedElement(), "The launch control must regain managed focus.");
        });
    }

    [TestMethod]
    public async Task TabStaysWithinTopmostDialogAndNestedCloseRestoresItsOrigin()
    {
        await RunAsync(900, false, async (window, host, origin) =>
        {
            var editor = new TextBox { Text = "Parent editor" };
            var parent = new ContentDialog { Title = "Parent", Content = editor, PrimaryButtonText = "Confirm", CloseButtonText = "Close" };
            var parentShow = parent.ShowAsync(host);
            await SettleAsync(window);
            Assert.IsTrue(editor.Focus());
            var child = new ContentDialog { Title = "Child", Content = new TextBox { Text = "Child editor" }, PrimaryButtonText = "Accept", CloseButtonText = "Return" };
            var childShow = child.ShowAsync(host);
            await SettleAsync(window);
            var visited = new HashSet<Control>();
            foreach (var modifiers in new[] { RawInputModifiers.None, RawInputModifiers.Shift })
                for (var press = 0; press < 8; press++)
                {
                    window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, "\t");
                    window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, "\t");
                    var focused = Assert.IsInstanceOfType<Control>(window.FocusManager.GetFocusedElement());
                    _ = visited.Add(focused);
                    Assert.IsTrue(ReferenceEquals(focused, child) || focused.GetVisualAncestors().Contains(child), $"Tab escaped the topmost dialog: {modifiers}/{press}, {focused.GetType().Name}/{focused.Name}, navigation={KeyboardNavigation.GetTabNavigation(child)}.");
                }
            Assert.IsTrue(visited.Contains((Control) child.Content!));
            Assert.IsTrue(visited.OfType<Button>().Any(button => button.Name is "PART_PrimaryButton"));
            Assert.IsTrue(visited.OfType<Button>().Any(button => button.Name is "PART_CloseButton"));
            Assert.IsTrue(child.Focus());
            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, "\t");
            window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, "\t");
            Assert.AreEqual("PART_CloseButton", Assert.IsInstanceOfType<Button>(window.FocusManager.GetFocusedElement()).Name);
            _ = await child.HideAsync(ContentDialogResult.Close);
            _ = await childShow.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreSame(editor, window.FocusManager.GetFocusedElement(), "Nested close must restore the parent editor, not discard its focus position.");
            _ = await parent.HideAsync();
            _ = await parentShow.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreSame(origin, window.FocusManager.GetFocusedElement());
        });
    }

    [TestMethod]
    public async Task OpenDialogSurvivesResizeThemeChangeAndDetachedLaunchControl()
    {
        await RunAsync(900, false, async (window, host, origin) =>
        {
            var dialog = new ContentDialog { Title = "Resize while open", Content = new TextBox { Text = "Keep my edit" }, CloseButtonText = "Return" };
            var shown = dialog.ShowAsync(host);
            await SettleAsync(window);
            foreach (var width in new[] { 360, 900 })
            {
                window.Width = width;
                window.RequestedThemeVariant = width is 360 ? ThemeVariant.Dark : ThemeVariant.Light;
                await SettleAsync(window);
                var surface = dialog.GetVisualDescendants().OfType<MaterialSurface>().Single(control => control.Name is "PART_LayoutRoot");
                var end = surface.TranslatePoint(new(surface.Bounds.Width, surface.Bounds.Height), window)!.Value;
                Assert.IsTrue(end.X <= width && end.Y <= window.Height);
                if (width is 360)
                {
                    Assert.AreEqual(window.Height, end.Y, 1);
                    var button = dialog.GetVisualDescendants().OfType<Button>().Single(control => control.Name is "PART_CloseButton");
                    Assert.AreEqual(surface.Bounds.Width - 48, button.Bounds.Width, 1, "Compact actions must use the full available row, including short labels.");
                }
                else
                    Assert.IsTrue(end.Y < window.Height);
                Assert.AreEqual("Keep my edit", ((TextBox) dialog.Content!).Text);
            }
            window.Content = new Button { Content = "Replacement page" };
            await SettleAsync(window);
            Click(window, dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name is "PART_CloseButton"));
            Assert.AreEqual(ContentDialogResult.Close, await shown.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.AreNotSame(origin, window.FocusManager.GetFocusedElement(), "Detached navigation entries must not regain focus.");
            Assert.IsFalse(host.IsOpen);
        });
    }

    [TestMethod]
    public async Task DialogWithoutInteractiveContentKeepsTabOnItsRoot()
    {
        await RunAsync(900, false, async (window, host, origin) =>
        {
            var dialog = new ContentDialog { Content = "Waiting for an external operation", IsLightDismissEnabled = false };
            var shown = dialog.ShowAsync(host);
            await SettleAsync(window);
            foreach (var modifiers in new[] { RawInputModifiers.None, RawInputModifiers.Shift })
            {
                window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, "\t");
                window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, "\t");
                Assert.AreSame(dialog, window.FocusManager.GetFocusedElement());
            }
            _ = await dialog.HideAsync();
            _ = await shown.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreSame(origin, window.FocusManager.GetFocusedElement());
        });
    }

    [TestMethod]
    public async Task RemovingLowerDialogDoesNotLoseTheOriginalFocusTarget()
    {
        await RunAsync(900, false, async (window, host, origin) =>
        {
            var parent = new ContentDialog { CloseButtonText = "Close" };
            var child = new ContentDialog { CloseButtonText = "Close" };
            var parentShow = parent.ShowAsync(host);
            await SettleAsync(window);
            var childShow = child.ShowAsync(host);
            await SettleAsync(window);
            _ = await parent.HideAsync();
            _ = await parentShow.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreSame(child, window.FocusManager.GetFocusedElement());
            _ = await child.HideAsync();
            _ = await childShow.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreSame(origin, window.FocusManager.GetFocusedElement());
        });
    }

    [TestMethod]
    public async Task BackdropRespectsLightDismissAndClosingVeto()
    {
        await RunAsync(900, true, async (window, host, origin) =>
        {
            var dialog = new ContentDialog { Title = "A persistent confirmation", CloseButtonText = "Close", IsLightDismissEnabled = false };
            var shown = dialog.ShowAsync(host);
            await SettleAsync(window);
            window.MouseDown(new(2, 2), MouseButton.Left);
            window.MouseUp(new(2, 2), MouseButton.Left);
            Assert.IsFalse(shown.IsCompleted);
            var veto = true;
            var requests = 0;
            dialog.Closing += (_, args) => { requests++; args.Cancel = veto; };
            dialog.IsLightDismissEnabled = true;
            window.MouseDown(new(2, 2), MouseButton.Left);
            window.MouseUp(new(2, 2), MouseButton.Left);
            Assert.AreEqual(1, requests);
            Assert.IsFalse(shown.IsCompleted);
            veto = false;
            window.MouseDown(new(2, 2), MouseButton.Left);
            window.MouseUp(new(2, 2), MouseButton.Left);
            Assert.AreEqual(ContentDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.AreEqual(2, requests);
            Assert.AreSame(origin, window.FocusManager.GetFocusedElement());
        });
    }

    private static async Task RunAsync(int width, bool dark, Func<Window, ContentDialogHost, Button, Task> verify)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            var origin = new Button { Content = "Open dialog" };
            var window = new Window { Width = width, Height = 640, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = origin };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                await SettleAsync(window);
                Assert.IsTrue(origin.Focus());
                await verify(window, new ContentDialogHost(window), origin);
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    private static async Task SettleAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        using var before = window.CaptureRenderedFrame();
        await Task.Delay(100);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        using var after = window.CaptureRenderedFrame();
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
        var hit = Assert.IsInstanceOfType<Visual>(window.InputHitTest(point));
        Assert.IsTrue(ReferenceEquals(hit, control) || hit.GetVisualAncestors().Contains(control), "The visible action must participate in pointer hit testing.");
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("PIXEVAL_DIALOG_CAPTURE") is not { Length: > 0 } directory)
            return;
        _ = Directory.CreateDirectory(directory);
        using var bitmap = window.CaptureRenderedFrame();
        Assert.IsNotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
}
