using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TokenizingBoxInputTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TextInputCommitsOnceAndKeepsUnfinishedText(bool dark)
    {
        await RunAsync(dark, (window, tokens, box, input, items) =>
        {
            tokens.AllowDuplicateTokens = false;
            List<object?> added = [];
            tokens.TokenAdded += (_, args) => added.Add(args.Item);
            window.KeyTextInput(" 中文 🌸 ");
            Render(window);
            Press(window, Key.Enter, PhysicalKey.Enter);
            CollectionAssert.AreEqual(new[] { "中文 🌸" }, items);
            Assert.AreEqual("", input.Text);
            Assert.HasCount(1, added);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Assert.HasCount(1, added, "Empty Enter does not add another token.");

            window.KeyTextInput("中文 🌸, second, unfinished");
            Render(window);
            CollectionAssert.AreEqual(new[] { "中文 🌸", "second" }, items);
            Assert.AreEqual("unfinished", input.Text);
            Assert.AreEqual(input.Text, tokens.Text);
            Assert.HasCount(2, added, "A duplicate is consumed without an extra add event.");
            Press(window, Key.Enter, PhysicalKey.Enter);
            CollectionAssert.AreEqual(new[] { "中文 🌸", "second", "unfinished" }, items);
            Assert.HasCount(3, added);
        });
    }

    [TestMethod]
    public async Task RejectedSeparatedTokenPreservesTheUncommittedSuffix()
    {
        await RunAsync(false, (window, tokens, box, input, items) =>
        {
            tokens.TokenSeparator = "::";
            tokens.TokenAdding += (_, args) => args.Cancel = Equals(args.Item, "rejected");
            window.KeyTextInput(" first :: rejected :: last");
            Render(window);
            CollectionAssert.AreEqual(new[] { "first" }, items);
            Assert.AreEqual(" rejected :: last", input.Text);
            Assert.AreEqual(input.Text, tokens.Text);
        });
    }

    [TestMethod]
    public async Task BackspaceAndRemoveCommandRespectVetoAndKeepInput()
    {
        await RunAsync(false, (window, tokens, box, input, items) =>
        {
            items.Add("protected");
            items.Add("removable");
            Render(window);
            List<object?> removed = [];
            tokens.TokenRemoving += (_, args) => args.Cancel = Equals(args.Item, "protected");
            tokens.TokenRemoved += (_, args) => removed.Add(args.Item);
            Assert.IsTrue(input.Focus());
            window.KeyTextInput("x");
            Render(window);
            Press(window, Key.Back, PhysicalKey.Backspace);
            Assert.AreEqual("", input.Text);
            Assert.HasCount(2, items, "Backspace edits nonempty text before removing tokens.");
            Press(window, Key.Back, PhysicalKey.Backspace);
            CollectionAssert.AreEqual(new[] { "protected" }, items);
            CollectionAssert.AreEqual(new object[] { "removable" }, removed);
            Press(window, Key.Back, PhysicalKey.Backspace);
            Assert.HasCount(1, items);
            var remove = tokens.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("tokenizing-token-remove"));
            remove.Command!.Execute(remove.CommandParameter);
            Assert.HasCount(1, items, "The remove button uses the same veto as Backspace.");
            Assert.HasCount(1, removed);
        });
    }

    [TestMethod]
    public async Task ReadOnlyAndFixedSizeSourcesNeverConsumeRejectedInput()
    {
        await RunAsync(false, (window, tokens, box, input, items) =>
        {
            foreach (var source in new System.Collections.IEnumerable[] { new[] { "fixed" }, new ReadOnlyCollection<string>(["read only"]) })
            {
                tokens.ItemsSource = source;
                tokens.Text = "keep";
                Render(window);
                Assert.IsTrue(input.Focus());
                Press(window, Key.Enter, PhysicalKey.Enter);
                Assert.AreEqual("keep", input.Text);
                Assert.AreEqual(1, tokens.TokenCount);
                Assert.IsFalse(tokens.RemoveToken(source.Cast<string>().Single()));
            }
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SelectedSuggestionVetoDoesNotFallBackToAddingItsText(bool dark)
    {
        await RunAsync(dark, (window, tokens, box, input, items) =>
        {
            var suggestion = new Suggestion("suggested");
            tokens.SuggestionItemsSource = new[] { suggestion };
            tokens.ItemSelector = (_, item) => ((Suggestion) item).Label;
            tokens.FilterMode = AutoCompleteFilterMode.None;
            tokens.Text = "typed";
            Render(window);
            box.IsDropDownOpen = true;
            Render(window);
            box.SelectedItem = suggestion;
            var calls = 0;
            tokens.TokenAdding += (_, args) =>
            {
                calls++;
                args.Cancel = ReferenceEquals(args.Item, suggestion);
            };
            Assert.AreSame(suggestion, box.SelectedItem);
            var textBefore = input.Text;
            Press(window, Key.Enter, PhysicalKey.Enter);
            Assert.IsEmpty(items, "A rejected selected item must not become an accepted raw string.");
            Assert.AreEqual(1, calls, "One Enter invokes the adding contract once.");
            Assert.AreEqual(textBefore, input.Text);
            Assert.AreSame(suggestion, box.SelectedItem, "A veto keeps the selected candidate available for correction.");
        });
    }

    [TestMethod]
    public async Task AcceptedSuggestionUsesTheTransformedItemAndClearsSelection()
    {
        await RunAsync(false, (window, tokens, box, input, items) =>
        {
            var suggestion = new Suggestion("suggested");
            tokens.SuggestionItemsSource = new[] { suggestion };
            tokens.FilterMode = AutoCompleteFilterMode.None;
            tokens.Text = "typed";
            Render(window);
            box.IsDropDownOpen = true;
            Render(window);
            box.SelectedItem = suggestion;
            var calls = 0;
            tokens.TokenAdding += (_, args) =>
            {
                calls++;
                Assert.AreSame(suggestion, args.Item);
                args.Item = suggestion.Label;
            };
            Press(window, Key.Enter, PhysicalKey.Enter);
            CollectionAssert.AreEqual(new[] { "suggested" }, items);
            Assert.AreEqual(1, calls);
            Assert.AreEqual("", input.Text);
            Assert.IsNull(box.SelectedItem);
            Assert.IsFalse(box.IsDropDownOpen);
        });
    }

    [TestMethod]
    public async Task HandledEnterDoesNotMutateTokens()
    {
        await RunAsync(false, (window, tokens, box, input, items) =>
        {
            window.KeyTextInput("keep");
            Render(window);
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Handled = true });
            Assert.IsEmpty(items);
            Assert.AreEqual("keep", input.Text);
        });
    }

    private static async Task RunAsync(bool dark, Action<Window, TokenizingBox, AutoCompleteBox, TextBox, ObservableCollection<string>> verify)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var items = new ObservableCollection<string>();
            var tokens = new TokenizingBox { ItemsSource = items, Width = 500 };
            var window = new Window { Width = 640, Height = 480, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = tokens };
            try
            {
                window.Show();
                Render(window);
                var box = tokens.GetVisualDescendants().OfType<AutoCompleteBox>().Single();
                var input = box.GetVisualDescendants().OfType<TextBox>().Single();
                Assert.IsTrue(input.Focus());
                verify(window, tokens, box, input, items);
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        }, default);
    }

    private static void Press(Window window, Key key, PhysicalKey physicalKey)
    {
        window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        Render(window);
    }

    private static void Render(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed record Suggestion(string Label);
}
