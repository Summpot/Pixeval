using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Styling;
using Material3Expressive.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.Views.Settings;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DateTimeOffsetSettingsValueTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        I18NManager.CandidatePaths.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval")));
        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EditorRebindsAndDetachesWithoutLosingExactOffsets(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var initial = new DateTimeOffset(2026, 11, 7, 10, 20, 30, TimeSpan.FromMinutes(330)).AddTicks(1234567);
            var first = ExtensionDateSettingTest.CreateEntry(initial);
            var second = ExtensionDateSettingTest.CreateEntry(initial.AddDays(1));
            var editor = new DateTimeOffsetSettingsValue { Entry = first };
            var window = new Window { Width = 600, Height = 300, Content = editor, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.AreEqual(initial.ToString("O", CultureInfo.InvariantCulture), editor.Text);
                first.Value = initial.ToOffset(TimeSpan.FromMinutes(-210));
                Assert.AreEqual(first.Value.ToString("O", CultureInfo.InvariantCulture), editor.Text, "Model-side offset-only updates must reach the editor.");

                editor.Text = "invalid";
                Assert.IsTrue(DataValidationErrors.GetHasErrors(editor));
                Assert.IsTrue(MaterialTextField.GetIsError(editor));
                Assert.AreEqual(I18NManager.GetResource(SettingsMainViewResources.InvalidDateTimeOffset), MaterialTextField.GetSupportingText(editor));
                Assert.AreEqual(I18NManager.GetResource(SettingsMainViewResources.InvalidDateTimeOffset), AutomationProperties.GetHelpText(editor));
                editor.Entry = second;
                Assert.IsFalse(DataValidationErrors.GetHasErrors(editor));
                var secondText = editor.Text;
                first.Value = initial.AddHours(1);
                Assert.AreEqual(secondText, editor.Text, "Replaced entries must no longer update the control.");

                window.Content = null;
                second.Value = initial.AddDays(2);
                Assert.AreEqual(secondText, editor.Text, "Detached controls must release model subscriptions.");
                window.Content = editor;
                window.UpdateLayout();
                Assert.AreEqual(second.Value.ToString("O", CultureInfo.InvariantCulture), editor.Text, "Reattachment must read the current value.");
                editor.DataContext = null;
                Assert.IsNull(editor.Text);
                second.Value = initial.AddDays(3);
                Assert.IsNull(editor.Text);

                var failure = new InvalidOperationException("Fixture rejected the date");
                var failNextWrite = true;
                var rejecting = ExtensionDateSettingTest.CreateEntry(initial, _ =>
                {
                    if (failNextWrite)
                        throw failure;
                });
                editor.Entry = rejecting;
                editor.Text = initial.AddHours(1).ToString("O", CultureInfo.InvariantCulture);
                Assert.IsTrue(DataValidationErrors.GetHasErrors(editor));
                Assert.IsTrue(DataValidationErrors.GetErrors(editor)!.Cast<object>().Any(error => ReferenceEquals(error, failure) || Equals(error, failure.Message)));
                failNextWrite = false;
                editor.Text = initial.AddHours(2).ToString("O", CultureInfo.InvariantCulture);
                Assert.IsFalse(DataValidationErrors.GetHasErrors(editor));
                Assert.IsTrue(rejecting.Value.EqualsExact(initial.AddHours(2)));
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        }, default);
    }
}
