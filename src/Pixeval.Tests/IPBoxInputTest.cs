using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement.Settings;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Settings.Entries;
using Pixeval.Utilities;
using Pixeval.Views.Settings;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class IPBoxInputTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval"));
        I18NManager.CandidatePaths.Add(projectPath);
        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddressListKeepsInvalidInputAndClearsObsoleteDuplicateFeedback(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var settings = new PixivDomainFrontingSettings { PixivAppApiNameResolver = ["192.0.2.1"] };
            var list = new IPListInput
            {
                Entry = new IPSetSettingsEntry<PixivDomainFrontingSettings>(settings, value => value.PixivAppApiNameResolver),
                Width = 360
            };
            var window = new Window { Width = 480, Height = 480, Content = list };
            try
            {
                window.Show();
                Render();
                var address = list.GetVisualDescendants().OfType<IPBox>().Single();
                var add = list.Children.OfType<DockPanel>().Single().Children.OfType<Button>().Single();
                var invalid = list.GetVisualDescendants().OfType<InfoBar>().Single(control => control.Name is "InvalidFormatInfoBar");
                var duplicate = list.GetVisualDescendants().OfType<InfoBar>().Single(control => control.Name is "DuplicatesInfoBar");
                Type("2001:db8::invalid");
                Activate(add);
                Assert.HasCount(1, settings.PixivAppApiNameResolver);
                Assert.AreEqual("2001:db8::invalid", address.Text);
                Assert.IsTrue(invalid.IsVisible);
                Type("2001:0db8:0000:0000:0000:0000:0000:0001");
                Activate(add);
                CollectionAssert.AreEqual(new[] { "192.0.2.1", "2001:db8::1" }, settings.PixivAppApiNameResolver);
                Assert.IsFalse(invalid.IsVisible);
                Activate(add);
                Assert.HasCount(2, settings.PixivAppApiNameResolver);
                Assert.IsTrue(duplicate.IsVisible);
                Type("198.51.100.2");
                Assert.IsFalse(duplicate.IsVisible, "Editing a different address must clear the previous duplicate result.");
                Type("2001:db8::1");
                Activate(add);
                Assert.IsTrue(duplicate.IsVisible);
                var duplicateRemove = list.GetVisualDescendants().OfType<Button>().Single(button => button.DataContext is "2001:db8::1");
                Activate(duplicateRemove);
                Assert.IsFalse(duplicate.IsVisible);
                Assert.HasCount(1, settings.PixivAppApiNameResolver);
                Activate(add);
                Activate(add);
                Assert.IsTrue(duplicate.IsVisible);
                var replacement = new PixivDomainFrontingSettings { PixivAppApiNameResolver = ["203.0.113.1"] };
                list.Entry = new IPSetSettingsEntry<PixivDomainFrontingSettings>(replacement, value => value.PixivAppApiNameResolver);
                Render();
                Assert.IsFalse(duplicate.IsVisible, "A new settings entry must not inherit the old entry's duplicate feedback.");
                var remove = list.GetVisualDescendants().OfType<Button>().Single(button => button.DataContext is "203.0.113.1");
                Activate(remove);
                Assert.IsEmpty(replacement.PixivAppApiNameResolver);
                Assert.HasCount(2, settings.PixivAppApiNameResolver);
            }
            finally
            {
                window.Close();
            }
            return;

            void Type(string text)
            {
                var input = list.GetVisualDescendants().OfType<IPBox>().Single().GetVisualDescendants().OfType<TextBox>().Single();
                Assert.IsTrue(input.Focus());
                input.SelectAll();
                window.KeyTextInput(text);
                Render();
            }

            void Activate(Button button)
            {
                Assert.IsTrue(button.Focus());
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Render();
            }

            void Render()
            {
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KeyboardEditingKeepsInvalidInputAndNormalizesOnlyValidAddresses(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var before = new Button { Content = "Before" };
            var address = new IPBox { Width = 320, Text = "192.0.2.1" };
            var after = new Button { Content = "After" };
            var window = new Window { Width = 480, Height = 320, Content = new StackPanel { Children = { before, address, after } } };
            try
            {
                window.Show();
                Render();
                var input = address.GetVisualDescendants().OfType<TextBox>().Single();
                Assert.IsTrue(before.Focus());
                Press(Key.Tab);
                Assert.IsTrue(input.IsFocused, "Tab enters the existing text editor, not a separate outer focus stop.");
                input.SelectAll();
                window.KeyTextInput("2001:db8::invalid");
                Render();
                Assert.IsFalse(address.IsValid);
                Assert.IsNull(address.IPAddress);
                Press(Key.Tab);
                Assert.IsTrue(after.IsFocused, "Tab can leave the embedded input.");
                Assert.AreEqual("2001:db8::invalid", address.Text, "Leaving invalid input must not normalize or discard it.");

                Press(Key.Tab, RawInputModifiers.Shift);
                Assert.IsTrue(input.IsFocused);
                input.SelectAll();
                window.KeyTextInput(" 2001:0db8:0000:0000:0000:0000:0000:0001 ");
                Render();
                Assert.IsTrue(address.IsValid);
                Assert.AreEqual(IPAddress.Parse("2001:db8::1"), address.IPAddress);
                Press(Key.Tab);
                Assert.AreEqual("2001:db8::1", address.Text);

                address.NormalizeOnLostFocus = false;
                Assert.IsTrue(input.Focus());
                input.SelectAll();
                window.KeyTextInput(" 2001:0db8::2 ");
                Press(Key.Tab);
                Assert.AreEqual(" 2001:0db8::2 ", address.Text);
                Assert.AreEqual(IPAddress.Parse("2001:db8::2"), address.IPAddress);
                address.NormalizeOnLostFocus = true;
                address.IPAddress = IPAddress.Parse("2001:db8::1");
                Render();

                Assert.IsTrue(input.Focus());
                input.SelectAll();
                window.KeyTextInput("192.0.2.2");
                Render();
                Press(Key.Z, RawInputModifiers.Control);
                Assert.AreEqual("2001:db8::1", address.Text, "Undo retains the existing native editor history.");
                Assert.AreEqual(IPAddress.Parse("2001:db8::1"), address.IPAddress);
                address.IsReadOnly = true;
                input.SelectAll();
                window.KeyTextInput("198.51.100.1");
                Render();
                Assert.AreEqual("2001:db8::1", address.Text);
                address.IsReadOnly = false;
                Press(Key.Back);
                Assert.AreEqual("", address.Text);
                Assert.IsTrue(address.IsValid);
                Assert.IsNull(address.IPAddress);
            }
            finally
            {
                window.Close();
            }
            return;

            void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
            {
                window.KeyPress(key, modifiers, PhysicalKey.None, null);
                window.KeyRelease(key, modifiers, PhysicalKey.None, null);
                Render();
            }

            void Render()
            {
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }, CancellationToken.None);
    }
}
