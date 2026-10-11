using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Views.Works;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WorkListBoxTest
{
    [TestMethod]
    public async Task LinedFlowReusesCardsAcrossRepeatedScrolls()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var builds = 0;
            var items = Enumerable.Range(0, 100).Select(i => i.ToString()).ToArray();
            var list = new WorkListBox
            {
                ItemsSource = items,
                ItemsPanel = new FuncTemplate<Panel?>(() => new Pixeval.Controls.VirtualizingWrapPanel
                {
                    ItemWidth = 100,
                    ItemHeight = 60,
                    CacheLength = 0
                })
            };
            list.DataTemplates.Add(new FuncDataTemplate<string>((_, _) =>
            {
                builds++;
                return new WorkItem { Content = new TextBlock { [!TextBlock.TextProperty] = new Binding(".") } };
            }));
            var window = new Window { Width = 400, Height = 240, Content = list };
            try
            {
                window.Show();
                window.UpdateLayout();
                ScrollTo(80);
                ScrollTo(0);
                var warmBuilds = builds;
                for (var i = 0; i < 3; i++)
                {
                    ScrollTo(80);
                    ScrollTo(0);
                }

                Assert.IsTrue(warmBuilds > 0);
                Assert.AreEqual(warmBuilds, builds);
            }
            finally
            {
                list.ItemsSource = null;
                window.Close();
            }

            void ScrollTo(int index)
            {
                list.ScrollIntoView(index);
                window.UpdateLayout();
                var container = (ListBoxItem) list.ContainerFromIndex(index)!;
                Assert.IsNotNull(container);
                var card = container.GetLogicalDescendants().OfType<WorkItem>().First();
                Assert.AreEqual(items[index], card.DataContext);
                Assert.AreEqual(items[index], ((TextBlock) card.Content!).Text);
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RecyclingRetainsCardAndRebindsItsChildren(bool detach)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var builds = 0;
            var list = new WorkListBox();
            list.DataTemplates.Add(new FuncDataTemplate<string>((_, _) =>
            {
                builds++;
                return new WorkItem { Content = new TextBlock { [!TextBlock.TextProperty] = new Binding(".") } };
            }));
            list.DataTemplates.Add(new FuncDataTemplate<int>((_, _) => new WorkItem()));
            var generator = list.ItemContainerGenerator;
            Assert.IsTrue(generator.NeedsContainer("first", 0, out var key));
            Assert.IsTrue(generator.NeedsContainer("second", 1, out var sameKey));
            Assert.AreSame(key, sameKey);
            Assert.IsTrue(generator.NeedsContainer(42, 2, out var otherKey));
            Assert.AreNotSame(key, otherKey);

            var container = (ListBoxItem) generator.CreateContainer("first", 0, key);
            generator.PrepareItemContainer(container, "first", 0);
            Assert.IsNotNull(container.ContentTemplate, "ContentTemplate should not be null");
            Assert.AreEqual("first", container.Content);
            var window = new Window { Content = container };
            try
            {
                window.Show();
                window.UpdateLayout();
                var card = container.GetLogicalDescendants().OfType<WorkItem>().First();
                var text = (TextBlock) card.Content!;
                Assert.AreEqual("first", text.Text);

                generator.ClearItemContainer(container);
                Assert.IsNull(card.DataContext);
                Assert.IsNull(container.DataContext);
                Assert.IsNull(text.Text);
                if (detach)
                    window.Content = null;

                generator.PrepareItemContainer(container, "second", 1);
                if (detach)
                    window.Content = container;
                window.UpdateLayout();
                var reboundCard = container.GetLogicalDescendants().OfType<WorkItem>().First();
                Assert.AreSame(card, reboundCard);
                Assert.AreEqual("second", text.Text);
                Assert.AreEqual(1, builds);
            }
            finally
            {
                generator.ClearItemContainer(container);
                window.Close();
            }
        }, CancellationToken.None);
    }
}
