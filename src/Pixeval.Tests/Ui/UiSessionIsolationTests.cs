using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Services.Core;
using Pixeval.Utilities;
using Pixeval.Views.Settings;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class UiSessionIsolationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SequentialApplicationsKeepTheirDataAndEditorStateIsolated()
    {
        Application? previousApp = null;
        object? previousServices = null;
        string? previousDirectory = null;
        int? uiThread = null;
        foreach (var culture in new[] { "zh-Hans", "ru-RU", "en-US" })
            await UiTestSession.RunAsync(TestContext, _ =>
            {
                Assert.AreNotSame(previousApp, Application.Current, "Each dispatch creates a new production App.");
                Assert.AreNotSame(previousServices, App.AppViewModel.AppServiceProvider, "Services must not leak across applications.");
                Assert.AreNotEqual(previousDirectory, AppInfo.ApplicationFolderPath);
                Assert.IsFalse(File.Exists(Path.Combine(AppInfo.ApplicationFolderPath, "session-marker")));
                Assert.AreEqual(uiThread ?? Environment.CurrentManagedThreadId, Environment.CurrentManagedThreadId,
                    "Sequential UI applications share one worker thread for dispatcher-affine third-party caches.");
                uiThread = Environment.CurrentManagedThreadId;
                previousApp = Application.Current;
                previousServices = App.AppViewModel.AppServiceProvider;
                previousDirectory = AppInfo.ApplicationFolderPath;
                File.WriteAllText(Path.Combine(previousDirectory, "session-marker"), culture);

                var page = UiTestSession.Create<NavigationSettingsPage>();
                var shell = App.CreateViewContainer();
                shell.NavigateTo(page);
                var window = new Window { Width = 1000, Height = 720, Content = shell };
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(window);
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                    var editor = page.FindControl<TextEditor>("YamlEditor")!;
                    const string yaml = "header:\n  - page: Search\nfooter:\n  - page: Settings\n";
                    editor.Text = yaml;
                    editor.TextArea.Caret.Offset = yaml.Length;
                    Assert.IsTrue(editor.TextArea.Focus());
                    window.KeyTextInput("# " + culture);
                    Assert.AreEqual(yaml + "# " + culture, editor.Text);
                    var modifiers = KeyboardShortcut.CreatePlatformCommandGesture(Key.Z).KeyModifiers.HasFlag(KeyModifiers.Meta)
                        ? RawInputModifiers.Meta : RawInputModifiers.Control;
                    window.KeyPress(Key.Z, modifiers, PhysicalKey.Z, "z");
                    window.KeyRelease(Key.Z, modifiers, PhysicalKey.Z, "z");
                    Assert.AreEqual(yaml, editor.Text, "The real editor retains its undo bindings in every application.");
                    window.KeyPress(Key.S, modifiers, PhysicalKey.S, "s");
                    window.KeyRelease(Key.S, modifiers, PhysicalKey.S, "s");
                    Assert.AreEqual(yaml, App.AppViewModel.NavigationMenuYamlText);
                    Assert.AreEqual(yaml.ReplaceLineEndings(Environment.NewLine),
                        File.ReadAllText(Path.Combine(AppInfo.SettingsFolder, "navigation_menu.yaml")));
                }
                finally { window.Close(); }
                Assert.AreEqual(0, shell.Pages.Count, "The production window close path clears the old application pages.");
                return Task.CompletedTask;
            }, culture);
    }
}
