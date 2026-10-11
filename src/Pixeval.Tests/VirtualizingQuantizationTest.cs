// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Layout;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using WrapPanelItemsAlignment = Pixeval.Controls.WrapPanelItemsAlignment;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class VirtualizingQuantizationTest
{
    [TestMethod]
    [DataRow(WrapPanelItemsAlignment.Start)]
    [DataRow(WrapPanelItemsAlignment.Center)]
    public async Task AdaptiveGridSubpixelOscillationMaintainsItemsPerLineAndSlotStability(WrapPanelItemsAlignment alignment)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var grid = new VirtualizingAdaptiveGrid
            {
                ItemSize = 100,
                ItemSpacing = 10,
                LineSize = 100,
                LineSpacing = 10,
                ItemsAlignment = alignment
            };
            var items = Enumerable.Range(0, 30).Select(i => i.ToString()).ToArray();
            var list = new ListBox
            {
                ItemsSource = items,
                ItemsPanel = new FuncTemplate<Panel?>(() => grid),
                ItemTemplate = new FuncDataTemplate<string>((_, _) => new Border { Width = 100, Height = 100 })
            };
            var window = new Window { Width = 400.25, Height = 300, Content = list };
            window.Show();
            window.UpdateLayout();

            var initialItemsPerLine = grid.ItemsPerLine;
            Assert.IsTrue(initialItemsPerLine > 0);

            var initialChild = grid.Children.OfType<Control>().FirstOrDefault();
            Assert.IsNotNull(initialChild);
            var initialOriginX = initialChild.Bounds.X;

            double[] widths = [400.25, 400.75, 400.5, 400.25];
            var scale = TopLevel.GetTopLevel(grid)?.RenderScaling ?? 1.0;
            var maxAllowedDelta = 1.0 / scale + 0.001;

            foreach (var w in widths)
            {
                window.Width = w;
                window.UpdateLayout();

                Assert.AreEqual(initialItemsPerLine, grid.ItemsPerLine, $"ItemsPerLine should remain identical for width {w}");

                var currentChild = grid.Children.OfType<Control>().FirstOrDefault();
                Assert.IsNotNull(currentChild);
                var delta = Math.Abs(currentChild.Bounds.X - initialOriginX);
                Assert.IsTrue(delta <= maxAllowedDelta, $"Slot origin shifted by {delta}, exceeding limit {maxAllowedDelta}");
            }

            window.Close();
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task WrapPanelSubpixelOscillationMaintainsSlotStability()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var panel = new VirtualizingWrapPanel
            {
                ItemWidth = 100,
                ItemHeight = 100,
                ItemSpacing = 10,
                LineSpacing = 10,
                ItemsAlignment = WrapPanelItemsAlignment.Center
            };
            var items = Enumerable.Range(0, 30).Select(i => i.ToString()).ToArray();
            var list = new ListBox
            {
                ItemsSource = items,
                ItemsPanel = new FuncTemplate<Panel?>(() => panel),
                ItemTemplate = new FuncDataTemplate<string>((_, _) => new Border { Width = 100, Height = 100 })
            };
            var window = new Window { Width = 400.25, Height = 300, Content = list };
            window.Show();
            window.UpdateLayout();

            var initialChild = panel.Children.OfType<Control>().FirstOrDefault();
            Assert.IsNotNull(initialChild);
            var initialOriginX = initialChild.Bounds.X;

            double[] widths = [400.25, 400.75, 400.5, 400.25];
            var scale = TopLevel.GetTopLevel(panel)?.RenderScaling ?? 1.0;
            var maxAllowedDelta = 1.0 / scale + 0.001;

            foreach (var w in widths)
            {
                window.Width = w;
                window.UpdateLayout();

                var currentChild = panel.Children.OfType<Control>().FirstOrDefault();
                Assert.IsNotNull(currentChild);
                var delta = Math.Abs(currentChild.Bounds.X - initialOriginX);
                Assert.IsTrue(delta <= maxAllowedDelta, $"Slot origin shifted by {delta}, exceeding limit {maxAllowedDelta}");
            }

            window.Close();
        }, CancellationToken.None);
    }
}
