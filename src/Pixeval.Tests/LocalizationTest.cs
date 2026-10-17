using System;
using System.Linq;
using System.Threading.Tasks;
using AutoSettingsPage;
using AutoSettingsPage.Models;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Views.Download;
using Pixeval.Views.Settings;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LocalizationTest
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerLoadingTest.ViewerApplication));
    [ClassCleanup]
    public static async Task Cleanup() => await Session.DisposeAsync();

    [TestMethod]
    public async Task LanguageSelectionAndPersistedNavigationKeysRemainCompatible()
    {
        var session = Session;
        await session.Dispatch(() =>
        {
            var previous = Strings.CurrentLocale;
            try
            {
                var localization = AppLocalization.Instance;
                var system = localization.ResolveLanguage("");
                Assert.AreEqual("en-US", localization.ResolveLanguage("EN-us"));
                Assert.AreEqual("en-US", localization.ResolveLanguage("en-GB"));
                Assert.AreEqual("zh-Hans", localization.ResolveLanguage("zh-CN"));
                Assert.AreEqual("zh-Hans", localization.ResolveLanguage("de-DE"));
                Assert.AreEqual(system, localization.ResolveLanguage("not a culture name!"));
                localization.SetLanguage("fr-FR");
                Assert.AreEqual("sample - 搜索", Strings.MainPageSearchResultFormatted(searchText: "sample"));
                Assert.AreEqual(system, localization.ResolveLanguage(""));
                Assert.IsTrue(MessageKeys.TryGet("MainPage.Tab.Settings", out var translated));
                Assert.AreEqual(Strings.MainPageTabSettings(), translated);
                Assert.IsFalse(MessageKeys.TryGet("unknown.key", out _));
                var parsed = NavigationYamlParser.Parse("header:\n  - page: Settings\n    title: $MainPage.Tab.Settings\nfooter: []");
                Assert.IsTrue(parsed.IsValid);
                Assert.AreEqual(Strings.MainPageTabSettings(), parsed.Configuration!.HeaderItems[0].Header);
            }
            finally { AppLocalization.Instance.SetLanguage(previous); }
        }, default);
    }

    [TestMethod]
    public async Task OpenHelpSettingsAndDownloadControlsUpdateWithoutLosingState()
    {
        var session = Session;
        await session.Dispatch(() =>
        {
            var previous = Strings.CurrentLocale;
            var settings = new ApplicationSettingsGroup { CultureName = "en-US" };
            LocalSettingsEntryHelper.Initialize();
            var entry = new LanguageSettingsEntry<ApplicationSettingsGroup>(settings, s => s.CultureName);
            var control = new LanguageSettingsValue { Entry = entry };
            var card = new AutoSettingsPage.Avalonia.StringSettingsCard { Entry = entry };
            var help = new HelpPage();
            var helpText = new MarkdownBox();
            helpText.Bind(Markdown.Avalonia.MarkdownScrollViewer.MarkdownProperty,
                CompiledBinding.Create<LocalizedHelp, string>(static resources => resources["DownloadMacroHelp"], LocalizedHelp.Instance));
            var download = new DownloadItem { DataContext = new Pixeval.Download.DownloadItemSnapshot() };
            var panel = new StackPanel { Children = { control, card, download, help, helpText } };
            var window = new Window { Content = panel, Width = 900, Height = 700 };
            using var refresh = AppLocalization.Instance.Subscribe(() => entry.RefreshLocalization());
            try
            {
                window.Show();
                foreach (var locale in Strings.SupportedLocales)
                {
                    AppLocalization.Instance.SetLanguage(locale);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Assert.AreEqual(Strings.AppSettingsAppLanguageEntryHeader(), entry.Header);
                    Assert.AreEqual("en-US", entry.Value);
                    Assert.AreEqual(Strings.MainPageTabHelp(), help.Header);
                    Assert.IsTrue(LocalizedHelp.Instance["DownloadMacroHelp"].Length > 0);
                    Assert.AreEqual(LocalizedHelp.Instance["DownloadMacroHelp"], helpText.Markdown);
                    Assert.IsTrue(download.GetVisualDescendants().OfType<TextBlock>()
                        .Any(text => text.Text == ((Pixeval.Download.DownloadItemSnapshot)download.DataContext!).ProgressMessage));
                }
            }
            finally { window.Close(); window.Content = null; AppLocalization.Instance.SetLanguage(previous); }
        }, default);
    }

    [TestMethod]
    public async Task RefreshingSettingsPreservesValuesCallbacksAndLiteralOverrides()
    {
        var session = Session;
        await session.Dispatch(() =>
        {
            var previous = Strings.CurrentLocale;
            try
            {
                LocalSettingsEntryHelper.Initialize();
                var settings = new ApplicationSettingsGroup { CultureName = "ru-RU" };
                var entry = new LanguageSettingsEntry<ApplicationSettingsGroup>(settings, s => s.CultureName);
                var changes = 0;
                entry.ValueChanged += _ => changes++;
                entry.Description = "custom description";
                AppLocalization.Instance.SetLanguage("en-US");
                SettingsLocalization.Refresh([entry]);
                Assert.AreEqual(Strings.AppSettingsAppLanguageEntryHeader(), entry.Header);
                Assert.AreEqual("custom description", entry.Description);
                Assert.AreEqual("ru-RU", entry.Value);
                Assert.AreEqual(0, changes);
                entry.Value = "fr-FR";
                Assert.AreEqual(1, changes);
            }
            finally { AppLocalization.Instance.SetLanguage(previous); }
        }, default);
    }

    [TestMethod]
    public async Task InjectedFilterEngineUsesCurrentLanguageAfterSwitching()
    {
        await Session.Dispatch(() =>
        {
            var previous = Strings.CurrentLocale;
            using var storage = new Pixeval.Storage.StorageEngine(":memory:");
            using var engines = new Pixeval.Filters.LocalizedFilterSession(storage);
            try
            {
                foreach (var locale in new[] { "zh-Hans", "en-US", "ru-RU" })
                {
                    AppLocalization.Instance.SetLanguage(locale);
                    var completion = engines.Current.Analyze("(", 1).Completions.First(c => c.DisplayText == "and");
                    Assert.AreEqual(Strings.FilterCompletionsAnd(), completion.Description);
                }
            }
            finally { AppLocalization.Instance.SetLanguage(previous); }
        }, default);
    }

    [TestMethod]
    public async Task EnumAndNavigationItemsRetainIdentityAndNotifyOncePerSwitch()
    {
        var session = Session;
        await session.Dispatch(() =>
        {
            var previous = Strings.CurrentLocale;
            try
            {
                AppLocalization.Instance.SetLanguage("zh-Hans");
                var items = SymbolComboBoxItem.GetValues<DownloadListOption>();
                var first = items[0];
                var notifications = 0;
                System.ComponentModel.PropertyChangedEventHandler handler = (_, _) => notifications++;
                first.PropertyChanged += handler;
                try
                {
                    var parsed = NavigationYamlParser.Parse("header:\n  - page: Settings\n    title: $MainPage.Tab.Settings\nfooter: []");
                    var navigation = parsed.Configuration!.HeaderItems[0];
                    AppLocalization.Instance.SetLanguage("en-US");
                    navigation.RefreshLocalization();
                    Assert.AreSame(first, items[0]);
                    Assert.AreEqual(1, notifications);
                    Assert.AreEqual(Strings.EnumDownloadListOptionAllQueued(), first.Description);
                    Assert.AreEqual(Strings.MainPageTabSettings(), navigation.Header);
                    Assert.AreEqual("$MainPage.Tab.Settings", navigation.HeaderSource);
                    // Setting the same language must not fire another change.
                    AppLocalization.Instance.SetLanguage("en-US");
                    Assert.AreEqual(1, notifications);
                }
                finally { first.PropertyChanged -= handler; }
            }
            finally { AppLocalization.Instance.SetLanguage(previous); }
        }, default);
    }

    [TestMethod]
    public void LanguageSubscriptionsDoNotKeepViewModelsAlive()
    {
        var weak = CreateWeakViewModel();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.IsFalse(weak.IsAlive);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateWeakViewModel() => new(new LocalizationProbeViewModel());

    private sealed class LocalizationProbeViewModel : Pixeval.ViewModels.ViewModelBase;
}
