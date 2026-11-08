using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Filters;
using Pixeval.Localization;
using Pixeval.Utilities;
using Pixeval.Views.Works;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WorkFilterInputTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {

        AppLocalization.Instance.SetLanguage("zh-Hans");
    }

    [TestMethod]
    public async Task EnterWithoutASelectedCompletionSubmitsTextAndCaretOnce()
    {
        await RunAsync((window, filter, box, editor) =>
        {
            filter.Text = "#中文 🌸";
            editor.CaretIndex = 3;
            var searches = 0;
            filter.SearchRequested += (_, args) =>
            {
                searches++;
                Assert.AreEqual("#中文 🌸", args.Text);
                Assert.AreEqual(3, args.CaretIndex);
            };
            Press(window, Key.Enter, PhysicalKey.Enter);
            Assert.AreEqual(1, searches);
            Assert.AreEqual("#中文 🌸", filter.Text);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EnterCommitsSelectedCompletionBeforeSubmitting(bool hintOnly)
    {
        await RunAsync((window, filter, box, editor) =>
        {
            filter.Text = "#to AND +ai";
            var completion = new FilterCompletionItem("touhou", "touhou", "#touhou AND +ai", new(1, 2), null, hintOnly, FilterCompletionKind.Value);
            filter.Analyze = (_, _) => new(false, true, [], [completion], null, null);
            filter.RefreshCompletions();
            box.IsDropDownOpen = true;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            box.SelectedItem = box.ItemsSource!.Cast<FilterCompletionItem>().Single();
            Assert.IsTrue(box.IsDropDownOpen, "The completion fixture must open its popup before Enter.");
            Assert.IsNotNull(box.SelectedItem);
            var searches = 0;
            filter.SearchRequested += (_, _) => searches++;

            Press(window, Key.Enter, PhysicalKey.Enter);

            Assert.AreEqual(hintOnly ? "#to AND +ai" : "#touhou AND +ai", filter.Text);
            Assert.AreEqual(0, searches);
            Assert.IsFalse(box.IsDropDownOpen);
            Assert.IsNull(box.SelectedItem);
            if (!hintOnly)
                Assert.AreEqual(filter.Text!.Length, filter.CaretIndex);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Assert.AreEqual(1, searches);
        });
    }

    [TestMethod]
    public async Task TabCyclesSuggestionsWithoutEditingOrSubmitting()
    {
        await RunAsync((window, filter, box, editor) =>
        {
            filter.Text = "#";
            filter.Analyze = (_, _) => new(false, true, [],
                [new("first", "first", "#first", new(1, 0), null, false, FilterCompletionKind.Value), new("second", "second", "#second", new(1, 0), null, false, FilterCompletionKind.Value)], null, null);
            filter.RefreshCompletions();
            var items = box.ItemsSource!.Cast<FilterCompletionItem>().ToArray();
            box.IsDropDownOpen = true;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(box.IsDropDownOpen, "The completion fixture must open its popup before Tab.");
            var searches = 0;
            filter.SearchRequested += (_, _) => searches++;

            Press(window, Key.Tab, PhysicalKey.Tab);
            Assert.AreEqual(items[0], box.SelectedItem);
            Press(window, Key.Tab, PhysicalKey.Tab);
            Assert.AreEqual(items[1], box.SelectedItem);
            Press(window, Key.Tab, PhysicalKey.Tab);
            Assert.AreEqual(items[0], box.SelectedItem);
            Assert.AreEqual("#", filter.Text);
            Assert.AreEqual(0, searches);
        });
    }

    [TestMethod]
    public async Task RemovingTheAnalyzerClearsStaleCompletionState()
    {
        await RunAsync((window, filter, box, editor) =>
        {
            filter.Text = "#";
            filter.Analyze = (_, _) => new(false, true, [], [new("fixture", "fixture", "#fixture", new(1, 0), null, false, FilterCompletionKind.Value)], null, null);
            filter.RefreshCompletions();
            box.SelectedItem = box.ItemsSource!.Cast<FilterCompletionItem>().Single();
            box.IsDropDownOpen = true;
            filter.Analyze = null;
            filter.RefreshCompletions();

            Assert.IsFalse(box.IsDropDownOpen);
            Assert.IsNull(box.SelectedItem);
            Assert.IsEmpty(box.ItemsSource!.Cast<object>());
            var searches = 0;
            filter.SearchRequested += (_, _) => searches++;
            Press(window, Key.Enter, PhysicalKey.Enter);
            Assert.AreEqual("#", filter.Text);
            Assert.AreEqual(1, searches);
        });
    }

    [TestMethod]
    public async Task DiagnosticSelectionIsClampedAndCanBeCleared()
    {
        await RunAsync((window, filter, box, editor) =>
        {
            filter.Text = "#中文";
            filter.HighlightDiagnostic(new(-10, 100));
            Assert.AreEqual(0, editor.SelectionStart);
            Assert.AreEqual(3, editor.SelectionEnd);
            Assert.AreEqual(3, filter.CaretIndex);
            Assert.AreSame(editor, window.FocusManager!.GetFocusedElement());
            filter.ClearSelection();
            Assert.AreEqual(3, editor.SelectionStart);
            Assert.AreEqual(3, editor.SelectionEnd);
            filter.HighlightDiagnostic(new FilterTextSpan(1, 0));
            Assert.AreEqual(1, editor.SelectionStart);
            Assert.AreEqual(2, editor.SelectionEnd);
        });
    }

    [TestMethod]
    public async Task AlreadyHandledEnterIsNotSubmittedAgain()
    {
        await RunAsync((window, filter, box, editor) =>
        {
            var searches = 0;
            filter.SearchRequested += (_, _) => searches++;
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Handled = true });
            Assert.AreEqual(0, searches);
        });
    }

    private static void Press(Window window, Key key, PhysicalKey physicalKey)
    {
        window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task RunAsync(Action<Window, WorkFilterAutoSuggestBox, AutoCompleteBox, TextBox> verify)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var filter = new WorkFilterAutoSuggestBox();
            var window = new Window { Width = 640, Height = 480, Content = filter };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var box = filter.GetVisualDescendants().OfType<AutoCompleteBox>().Single();
                var editor = box.GetVisualDescendants().OfType<TextBox>().Single();
                Assert.IsTrue(editor.Focus());
                verify(window, filter, box, editor);
            }
            finally
            {
                window.Close();
            }
        }, default);
    }
}
