using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Common;
using Pixeval.Common.Collections;
using Pixeval.Controls;
using Pixeval.Mako;
using Pixeval.Views.Works;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class VirtualizingMasonryPanelTest
{
    [TestMethod]
    public void DeclaredSizeUsesIllustrationDimensions()
    {
        Assert.IsTrue(ItemNaturalSize.TryGet(DesignHelper.DesignIllustration, out var size));
        Assert.AreEqual(800, size.Width);
        Assert.AreEqual(600, size.Height);
    }

    [TestMethod]
    public void DeclaredSizeIgnoresItemsWithoutDimensions()
    {
        Assert.IsFalse(ItemNaturalSize.TryGet("nope", out _));
        Assert.IsFalse(ItemNaturalSize.TryGet(DesignHelper.DesignIllustration with { Width = 0, Height = 0 }, out _));
    }

    [TestMethod]
    public async Task MasonryDoesNotBuildOffscreenCardsWhenPageArrives()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var builds = 0;
            var items = new ObservableRangeCollection<object>();
            foreach (var item in CreateIllustrations(0, 40))
                items.Add(item);

            var list = new WorkListBox
            {
                ItemsSource = items,
                ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingMasonryPanel
                {
                    ColumnWidth = 200,
                    ColumnSpacing = 5,
                    RowSpacing = 5,
                    CacheLength = 0
                })
            };
            list.DataTemplates.Add(new FuncDataTemplate<Illustration>((_, _) =>
            {
                builds++;
                return new WorkItem { Width = 200, Height = 120 };
            }));
            var window = new Window { Width = 420, Height = 360, Content = list };
            try
            {
                window.Show();
                window.UpdateLayout();

                Assert.IsGreaterThan(0, builds);
                Assert.IsLessThan(40, builds);
                var buildsAfterFirstPage = builds;

                items.AddRange(CreateIllustrations(40, 20).ToArray());
                window.UpdateLayout();

                Assert.AreEqual(buildsAfterFirstPage, builds);
                Assert.AreEqual(60, items.Count);
            }
            finally
            {
                list.ItemsSource = null;
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task LinedFlowDoesNotBuildOffscreenCardsWhenPageArrives()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var builds = 0;
            var items = new ObservableRangeCollection<object>();
            foreach (var item in CreateIllustrations(0, 40))
                items.Add(item);

            var list = new WorkListBox
            {
                ItemsSource = items,
                ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingWrapPanel
                {
                    ItemHeight = 80,
                    ItemSpacing = 5,
                    LineSpacing = 5,
                    CacheLength = 0
                })
            };
            list.DataTemplates.Add(new FuncDataTemplate<Illustration>((_, _) =>
            {
                builds++;
                return new WorkItem { Width = 80, Height = 80 };
            }));
            var window = new Window { Width = 420, Height = 240, Content = list };
            try
            {
                window.Show();
                window.UpdateLayout();

                Assert.IsGreaterThan(0, builds);
                Assert.IsLessThan(40, builds);
                var buildsAfterFirstPage = builds;

                items.AddRange(CreateIllustrations(40, 20).ToArray());
                window.UpdateLayout();

                Assert.AreEqual(buildsAfterFirstPage, builds);
            }
            finally
            {
                list.ItemsSource = null;
                window.Close();
            }
        }, CancellationToken.None);
    }

    private static object[] CreateIllustrations(int start, int count) =>
        [.. Enumerable.Range(start, count).Select(i => (object) (DesignHelper.DesignIllustration with
        {
            Id = i + 1,
            Width = 400,
            Height = 200 + (i % 5) * 80
        }))];
}
