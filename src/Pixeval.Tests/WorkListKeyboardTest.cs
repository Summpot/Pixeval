using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.Views.Work;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WorkListKeyboardTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OpaqueCardsKeepKeyboardFocusVisibleThroughSelectionAndRecycling(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            var list = new WorkListBox
            {
                ItemsSource = Enumerable.Range(0, 100).Select(index => $"Artwork {index} 中文").ToArray(),
                SelectionMode = SelectionMode.Multiple,
                AutoScrollToSelectedItem = false,
                ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingWrapPanel { ItemWidth = 140, ItemHeight = 100, CacheLength = 0 })
            };
            list.Styles.Add(new ItemsViewListBoxStyles());
            list.DataTemplates.Add(new FuncDataTemplate<string>((_, _) => new WorkItem
            {
                Background = Brushes.DarkSlateGray,
                Content = new TextBlock { Foreground = Brushes.White, [!TextBlock.TextProperty] = new Binding(".") }
            }));
            var exit = new Button { Content = "Outside the list" };
            var window = new Window
            {
                Width = 460,
                Height = 360,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new DockPanel { Children = { new Border { [DockPanel.DockProperty] = Dock.Bottom, Child = exit }, list } }
            };
            try
            {
                window.Show();
                Render(window);
                var first = (ListBoxItem) list.ContainerFromIndex(0)!;
                Assert.IsTrue(first.Focus(NavigationMethod.Tab));
                Render(window);
                var ring = first.GetVisualDescendants().OfType<Border>().SingleOrDefault(border => border.Name == "KeyboardFocusBorder");
                Assert.IsNotNull(ring, "The card container needs a visible keyboard focus surface above opaque content.");
                Assert.IsTrue(ring.IsEffectivelyVisible);
                Assert.IsFalse(ring.IsHitTestVisible);
                Assert.IsTrue(ring.BorderThickness.Left >= 2 && ring.Bounds.Width > 0 && ring.Bounds.Height > 0);
                Assert.IsTrue(ring.BorderBrush is ISolidColorBrush { Color.A: > 0 });
                var initial = ((ISolidColorBrush) ring.BorderBrush!).Color;

                Press(window, Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Control);
                Render(window);
                var second = (ListBoxItem) list.ContainerFromIndex(1)!;
                Assert.IsTrue(second.IsFocused, "Ctrl+Right navigates without replacing the selection.");
                Assert.IsFalse(ring.IsEffectivelyVisible);
                var secondRing = second.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "KeyboardFocusBorder");
                Assert.IsTrue(secondRing.IsEffectivelyVisible);
                Press(window, Key.Space, PhysicalKey.Space);
                Render(window);
                Assert.IsTrue(second.IsSelected);
                Assert.IsTrue(secondRing.IsEffectivelyVisible, "Selection tint cannot hide the keyboard focus surface.");
                Capture(window, $"focus-selected-{dark}");

                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
                Render(window);
                Assert.AreNotEqual(initial, ((ISolidColorBrush) secondRing.BorderBrush!).Color, "Focus follows the local theme.");
                Assert.IsTrue(exit.Focus(NavigationMethod.Tab));
                Render(window);
                Assert.IsFalse(secondRing.IsEffectivelyVisible, "An unfocused selected item must not retain a focus ring.");
                list.ScrollIntoView(90);
                await Task.Delay(750);
                Render(window);
                var last = (ListBoxItem) list.ContainerFromIndex(90)!;
                Assert.IsNotNull(last, $"Offset {list.Scroll?.Offset}; extent {list.Scroll?.Extent}; viewport {list.Scroll?.Viewport}");
                Assert.IsTrue(last.Focus(NavigationMethod.Directional));
                Render(window);
                Assert.IsTrue(last.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "KeyboardFocusBorder").IsEffectivelyVisible);
                Assert.AreEqual("Artwork 90 中文", ((TextBlock) ((WorkItem) last.Content!).Content!).Text);
                Capture(window, $"focus-recycled-{dark}");
            }
            finally
            {
                list.ItemsSource = null;
                window.Content = null;
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
    }

    private static void Render(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("PIXEVAL_WORK_FOCUS_CAPTURE") is not { Length: > 0 } directory)
            return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
}
