using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PagePreviewSliderInputTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KeyboardPointerAndAutomationSelectExactlyOnePage(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.DispatchAsync(async () =>
        {
            var slider = new PagePreviewSlider { ItemsSource = new object[] { "one", "two", "three" } };
            var before = new Button { Content = "Before" };
            var after = new Button { Content = "After" };
            var panel = new StackPanel { Children = { before, slider, after } };
            var window = new Window { Width = 320, Height = 480, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = panel };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                await RenderAsync(window);
                Assert.IsTrue(before.Focus());
                Press(window, Key.Tab, PhysicalKey.Tab);
                Assert.IsTrue(slider.IsFocused, "Tab must enter the page selector through the real focus order.");
                Assert.IsTrue(slider.Bounds.Height >= 48, "The complete selector provides a usable pointer target.");
                var unhandledArrows = 0;
                panel.KeyDown += (_, e) =>
                {
                    if (!e.Handled && e.Key is Key.Left or Key.Right) unhandledArrows++;
                };
                Press(window, Key.Right, PhysicalKey.ArrowRight);
                Assert.AreEqual(1, slider.SelectedIndex);
                Press(window, Key.End, PhysicalKey.End);
                Assert.AreEqual(2, slider.SelectedIndex);
                Press(window, Key.Right, PhysicalKey.ArrowRight);
                Assert.AreEqual(2, slider.SelectedIndex);
                Assert.AreEqual(0, unhandledArrows, "A boundary arrow must not bubble into next-work navigation.");
                Press(window, Key.Home, PhysicalKey.Home);
                Assert.AreEqual(0, slider.SelectedIndex);
                Press(window, Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Control);
                Assert.AreEqual(0, slider.SelectedIndex, "Modified shortcuts do not edit the selector.");
                Assert.AreEqual(1, unhandledArrows);
                Press(window, Key.Tab, PhysicalKey.Tab);
                Assert.IsTrue(after.IsFocused, "Tab leaves the selector normally.");
                Press(window, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Shift);
                Assert.IsTrue(slider.IsFocused);

                var track = slider.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Track");
                var point = track.TranslatePoint(new Point(track.Bounds.Width - 1, track.Bounds.Height / 2), window)!.Value;
                window.MouseMove(point);
                await RenderAsync(window);
                Assert.AreEqual(0, slider.SelectedIndex, "Hover previews without navigating.");
                Assert.AreEqual(2, slider.PreviewIndex);
                window.MouseDown(point, MouseButton.Left);
                Assert.AreEqual(2, slider.SelectedIndex);
                window.MouseMove(new Point(-20, point.Y));
                window.MouseUp(new Point(-20, point.Y), MouseButton.Left);
                Assert.AreEqual(0, slider.SelectedIndex, "Captured dragging clamps to the first page outside the track.");
                window.MouseMove(new Point(20, 450));
                await RenderAsync(window);
                Assert.AreEqual(-1, slider.PreviewIndex);
                Assert.AreEqual("1/3", slider.IndicatorText);

                var peer = ControlAutomationPeer.CreatePeerForElement(slider)!;
                Assert.AreEqual(AutomationControlType.Slider, peer.GetAutomationControlType());
                Assert.IsFalse(string.IsNullOrWhiteSpace(peer.GetName()));
                var range = (IRangeValueProvider) peer;
                Assert.AreEqual(1d, range.Minimum);
                Assert.AreEqual(3d, range.Maximum);
                range.SetValue(2);
                Assert.AreEqual(1, slider.SelectedIndex, "Automation uses one-based image page numbers.");
                Assert.AreEqual(2d, range.Value);
                window.MouseDown(point, MouseButton.Left);
                Assert.AreEqual(2, slider.SelectedIndex);
                range.SetValue(2);
                window.MouseUp(new Point(-20, point.Y), MouseButton.Left);
                Assert.AreEqual(1, slider.SelectedIndex, "An automation selection cancels the previous pointer drag.");
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                panel.IsEnabled = false;
                Assert.IsTrue(range.IsReadOnly, "Disabling an ancestor also makes the selector read-only.");
                Assert.AreEqual(-1, slider.PreviewIndex, "Disabling an ancestor closes the captured preview.");
                window.MouseUp(new Point(-20, point.Y), MouseButton.Left);
                panel.IsEnabled = true;
                Assert.AreEqual(2, slider.SelectedIndex, "A cancelled drag cannot resume when its ancestor is enabled again.");
                range.SetValue(2);
                slider.IsEnabled = false;
                Assert.IsTrue(range.IsReadOnly);
                Assert.Throws<InvalidOperationException>(() => range.SetValue(3));
                Assert.AreEqual(1, slider.SelectedIndex);
                slider.IsEnabled = true;
                Assert.Throws<ArgumentOutOfRangeException>(() => range.SetValue(99));
                Assert.Throws<ArgumentOutOfRangeException>(() => range.SetValue(double.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => range.SetValue(1.5));
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public async Task SourceReplacementAndDetachCancelPreviewsAndReleaseObservers()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.DispatchAsync(async () =>
        {
            var first = new TrackedItems("one", "two", "three");
            var second = new TrackedItems("four", "five");
            var slider = new PagePreviewSlider { ItemsSource = first };
            Popup? popup = null;
            slider.TemplateApplied += (_, e) => popup = e.NameScope.Find<Popup>("PART_PreviewPopup");
            var panel = new StackPanel { Children = { slider } };
            var window = new Window { Width = 320, Height = 480, Content = panel };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                await RenderAsync(window);
                Assert.AreEqual(1, first.SubscriberCount);
                var track = slider.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Track");
                var point = track.TranslatePoint(new Point(track.Bounds.Width - 1, track.Bounds.Height / 2), window)!.Value;
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                await RenderAsync(window);
                Assert.IsNotNull(popup);
                Assert.IsTrue(popup.IsOpen);
                slider.ItemsSource = second;
                Assert.AreEqual(0, first.SubscriberCount, "A replaced source must not retain the control.");
                Assert.AreEqual(1, second.SubscriberCount);
                Assert.IsFalse(popup.IsOpen, "Changing works closes the obsolete thumbnail popup.");
                Assert.AreEqual(-1, slider.PreviewIndex);
                Assert.IsNull(slider.PreviewItem);
                Assert.AreEqual(1, slider.SelectedIndex, "A smaller source bounds the selected page.");
                window.MouseUp(new Point(-20, point.Y), MouseButton.Left);
                Assert.AreEqual(1, slider.SelectedIndex, "The old drag release cannot choose a page in the replacement work.");

                for (var iteration = 0; iteration < 3; iteration++)
                {
                    panel.Children.Remove(slider);
                    Assert.AreEqual(0, second.SubscriberCount, "Detaching releases the observed source.");
                    Assert.IsFalse(popup.IsOpen);
                    second.Replace("only");
                    panel.Children.Add(slider);
                    await RenderAsync(window);
                    Assert.AreEqual(1, second.SubscriberCount, "Reattachment observes the source exactly once.");
                    Assert.IsFalse(slider.IsVisible, "Changes while detached are reconciled when the control returns.");
                    second.Replace("four", "five");
                    await RenderAsync(window);
                    Assert.IsTrue(slider.IsVisible);
                    Assert.AreEqual(0, slider.SelectedIndex);
                }
                window.Close();
                Assert.AreEqual(0, second.SubscriberCount, "Closing the window removes the final subscription.");
            }
            finally { window.Close(); }
        });
    }

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(80);
        window.UpdateLayout();
    }

    private sealed class TrackedItems(params object[] items) : IReadOnlyList<object>, INotifyCollectionChanged
    {
        private object[] _items = items;
        private NotifyCollectionChangedEventHandler? _changed;
        internal int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }
        internal void Replace(params object[] replacement)
        {
            _items = replacement;
            _changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
        public int Count => _items.Length;
        public object this[int index] => _items[index];
        public IEnumerator<object> GetEnumerator() => ((IEnumerable<object>) _items).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
