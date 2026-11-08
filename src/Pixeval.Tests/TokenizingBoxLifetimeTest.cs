using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TokenizingBoxLifetimeTest
{
    [TestMethod]
    public async Task LongLivedSettingsCollectionDoesNotRetainDiscardedEditor()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var items = new ObservableCollection<string> { "retained setting" };
            var editor = CreateWeakEditor(items);
            for (var attempt = 0; attempt < 4 && editor.IsAlive; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            Assert.IsFalse(editor.IsAlive, "The settings collection must not own the lifetime of a discarded editor.");
            items.Add("still usable after editor collection");
            Assert.HasCount(2, items);
            GC.KeepAlive(items);
        }, default);
    }

    [TestMethod]
    public async Task LiveEditorKeepsCollectionUpdatesAcrossRebindingAndRemounting()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var first = new ObservableCollection<string> { "first" };
            var second = new ObservableCollection<string> { "second" };
            var editor = new TokenizingBox { ItemsSource = first, AllowDuplicateTokens = false };
            var window = new Window { Content = editor };
            try
            {
                first.Add("before mounting");
                Assert.AreEqual(2, editor.TokenCount);
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                window.UpdateLayout();
                editor.ItemsSource = second;
                first.Add("old source");
                Assert.AreEqual(1, editor.TokenCount);
                second.Add("attached");
                Assert.AreEqual(2, editor.TokenCount);
                window.Content = null;
                second.Add("detached but retained editor");
                Assert.AreEqual(3, editor.TokenCount);
                window.Content = editor;
                window.UpdateLayout();
                Assert.IsTrue(editor.AddToken("new"));
                Assert.IsTrue(editor.AddToken("new"));
                Assert.AreEqual(4, editor.TokenCount);
                Assert.HasCount(4, second);
                Assert.IsTrue(editor.RemoveToken("new"));
                Assert.AreEqual(3, editor.TokenCount);
                editor.ItemsSource = null;
                second.Add("unbound");
                Assert.AreEqual(0, editor.TokenCount);
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        }, default);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateWeakEditor(ObservableCollection<string> items) =>
        new(new TokenizingBox { ItemsSource = items });
}
