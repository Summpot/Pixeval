using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FloatingPaneKeyboardTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KeyboardFocusRevealsFloatingPaneAndPreservesDocking(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            var content = new Button { Content = "Viewer content" };
            var action = new Button { Content = "Detail action" };
            var view = new FloatingPaneView
            {
                Content = content,
                Pane = action,
                PaneHorizontalAlignment = HorizontalAlignment.Right,
                PaneVerticalAlignment = VerticalAlignment.Bottom
            };
            var window = new Window { Width = 900, Height = 640, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = view };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                await RenderAsync(window);
                var pane = view.GetVisualDescendants().OfType<Border>().Single(control => control.Name is "PART_FloatingPaneBorder");
                window.MouseMove(new(2, 2));
                await WaitForOpacityAsync(window, pane, 0);
                Assert.IsTrue(content.Focus());
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
                window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
                Assert.AreSame(action, window.FocusManager.GetFocusedElement());
                await WaitForOpacityAsync(window, pane, 1);
                var calls = 0;
                action.Click += (_, _) => calls++;
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Assert.AreEqual(1, calls);

                Assert.IsTrue(content.Focus());
                await WaitForOpacityAsync(window, pane, 0);
                view.IsLocked = true;
                await WaitForOpacityAsync(window, pane, 0.9);
                Assert.IsTrue(action.Focus(NavigationMethod.Tab));
                await WaitForOpacityAsync(window, pane, 1);
                view.IsDocked = true;
                await RenderAsync(window);
                Assert.IsTrue(content.Focus());
                await WaitForOpacityAsync(window, pane, 1);
                Assert.IsFalse(FloatingPane.GetIsEnabled(pane), "Docked panes must retain their existing proximity opt-out.");
                Assert.AreEqual(1, view.DockProgress);
                view.IsLocked = false;
                view.IsDocked = false;
                window.MouseMove(new(2, 2));
                await WaitForOpacityAsync(window, pane, 0);
                Assert.AreEqual(1, calls, "Changing pane visibility must not invoke its actions.");
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    private static async Task WaitForOpacityAsync(Window window, Border pane, double expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (Math.Abs(pane.Opacity - expected) > 0.01 && DateTime.UtcNow < deadline)
            await RenderAsync(window);
        Assert.AreEqual(expected, pane.Opacity, 0.01, $"Pane visibility did not follow pointer/focus state: {string.Join(',', pane.Classes)}.");
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(100);
    }
}
