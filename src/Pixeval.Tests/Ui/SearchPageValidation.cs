using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Material3Expressive.Components;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Database;
using Pixeval.Models.Database.Managers;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Search;

namespace Pixeval.Tests.Ui;

internal static class SearchPageValidation
{
    private const string Pinned = "固定标签 with a long unbroken suffix ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string History = "历史关键词 with a translation that must remain visible in a compact window";

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        const string imageUri = "https://i.pximg.net/m3e/search-trending.png";
        using (var source = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var bytes = new MemoryStream())
        {
            source.CopyTo(bytes);
            fixture.ImageResponses[imageUri] = bytes.ToArray();
        }
        fixture.TrendingResponseFactory = uri => JsonSerializer.Serialize(new
        {
            trend_tags = new[]
            {
                new TrendingTag
                {
                    Tag = uri.AbsolutePath.EndsWith("novel", StringComparison.Ordinal) ? "小说 热门 tag" : "插画 热门 tag",
                    TranslatedName = "Trending translation",
                    Illustration = Illustration.CreateDefault() with
                    {
                        Id = 909001, Title = "Trending fixture", Width = 128, Height = 128,
                        ThumbnailUrls = new() { Medium = imageUri, Large = imageUri, SquareMedium = imageUri }
                    }
                }
            }
        });
        await client.SetTokenAsync("fixture-search-page");
        await client.ValidateTokenAsync();
        var persistence = App.AppViewModel.HistoryPersistHelper;
        await persistence.RestoreTask;
        var historyManager = App.AppViewModel.AppServiceProvider.GetRequiredService<SearchHistoryPersistentManager>();
        var pinned = App.AppViewModel.AppSettings.BrowsingExperienceSettings.PinnedTags;
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
            {
                pinned.Clear();
                persistence.SearchHistoryEntries.Clear();
                pinned.Add(Pinned);
                persistence.AddSearchHistory(History, "翻译与原文 separate display text");
                var model = SearchPageViewModel.Instance;
                model.SearchText = "";
                model.SelectedTrendingTagsType = SimpleWorkType.Illustration;
                model.SelectedAdvancedOptionsType = SimpleWorkType.Illustration;
                var page = new SearchPage();
                var navigation = new NavigationPage();
                await navigation.PushAsync(page);
                List<(Type Type, object? Header, object? WorkType)> routes = [];
                var container = new PageTestHost
                {
                    Content = navigation,
                    NavigationObserver = target => routes.Add((target.GetType(), target.Header, target.FindControl<SymbolComboBox>("SimpleWorkTypeComboBox")?.SelectedValue))
                };
                var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
                var scenario = $"{theme.Key}-{width}";
                try
                {
                    window.Show();
                    await UntilAsync(() => model.IllustrationTrendingTags.Count == 1 && model.NovelTrendingTags.Count == 1);
                    await RenderAsync(window);
                    var input = page.FindControl<AutoCompleteBox>("SearchAutoCompleteBox")!;
                    HorizontalBounds(window, input);
                    HorizontalBounds(window, (Control) input.InnerRightContent!);
                    Require(AutomationProperties.GetName(input) == I18NManager.GetResource(SearchResources.SearchBox.PlaceholderText)
                        && AutomationProperties.GetName((Control) input.InnerRightContent!) == I18NManager.GetResource(MainPageResources.Tab.Search),
                        "Search input and action expose localized names for their actual purpose");
                    Save(window, directory, "search-page-before-" + scenario);
                    var pinnedChip = Chip(Pinned);
                    var historyChip = Chip(History);
                    foreach (var chip in new[] { pinnedChip, historyChip })
                    {
                        HorizontalBounds(window, chip);
                        foreach (var text in chip.GetVisualDescendants().OfType<TextBlock>())
                        {
                            HorizontalBounds(window, text);
                            Require(text.TextLayout.Width <= text.Bounds.Width + 1 && text.TextLayout.Height <= text.Bounds.Height + 1
                                && text.TextLayout.TextLines.All(line => !line.HasCollapsed), "Long search tags and their translations remain completely readable");
                        }
                    }

                    await InvokeContextAsync(pinnedChip, SearchResources.SearchHistory.Copy);
                    Require(await window.Clipboard!.TryGetTextAsync() == Pinned, "Pinned copy keeps the full original text in the headless clipboard");
                    await InvokeContextAsync(historyChip, SearchResources.SearchHistory.AddPinnedTag);
                    Require(pinned.Contains(History) && historyManager.GetByValue(History) is null, "Pinning moves exactly that search history entry into pinned settings");
                    await InvokeContextAsync(Chip(History), SearchResources.SearchHistory.Unpin);
                    Require(!pinned.Contains(History) && historyManager.GetByValue(History) is not null, "Unpinning restores the tag to persisted history");
                    AppInfo.SaveAppSettings(App.AppViewModel.AppSettings);
                    Require(AppInfo.LoadAppSettings(App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>())!.BrowsingExperienceSettings.PinnedTags.SequenceEqual(pinned), "Pinned values round-trip through the unchanged settings format");

                    await ActivateAsync(Chip(Pinned));
                    Require(routes.Count == 1 && model.SearchText == Pinned && routes[^1].Type == typeof(WorkSearchResultPage)
                        && Equals(routes[^1].WorkType, SimpleWorkType.Illustration), "A pinned tag starts exactly one illustration search with its complete keyword");
                    container.CloseDestinationAndReturn();
                    await ActivateAsync(Chip(History));
                    Require(routes.Count == 2 && model.SearchText == History, "History search submits exactly once and preserves the complete keyword");
                    container.CloseDestinationAndReturn();
                    await InvokeContextAsync(Chip(History), SearchResources.SearchHistory.Delete);
                    Require(historyManager.GetByValue(History) is null && !persistence.SearchHistoryEntries.Any(entry => entry.Value == History),
                        "Deleting history removes only the selected persisted item");

                    var trendingType = page.GetVisualDescendants().OfType<TabStrip>().Single();
                    await RevealAsync(trendingType);
                    HorizontalBounds(window, trendingType);
                    foreach (var tab in trendingType.GetRealizedContainers())
                        HorizontalBounds(window, tab);
                    var novelTab = (TabStripItem) trendingType.ContainerFromIndex(1)!;
                    Require(novelTab.Focus(NavigationMethod.Tab), "Trending type is keyboard reachable");
                    Press(window, Key.Space, PhysicalKey.Space);
                    await RenderAsync(window);
                    Require(model.SelectedTrendingTagsType == SimpleWorkType.Novel, "Trending type keyboard selection changes the real data source");
                    var card = page.GetVisualDescendants().OfType<Card>().Single(control => control.DataContext is TrendingTag);
                    await ActivateAsync(card);
                    Require(routes.Count == 3 && Equals(routes[^1].WorkType, SimpleWorkType.Novel)
                        && Equals(routes[^1].Header, I18NManager.GetResource(MainPageResources.SearchResultFormatted, "小说 热门 tag")),
                        "Trending action uses its own work type and tag exactly once");
                    container.CloseDestinationAndReturn();
                    var expander = page.FindControl<Expander>("AdvancedExpander")!;
                    expander.IsExpanded = true;
                    await RenderAsync(window);
                    var tabs = page.FindControl<TabbedPage>("AdvancedOptionsTabControl")!;
                    model.SelectedAdvancedOptionsType = SimpleWorkType.Novel;
                    await RenderAsync(window);
                    Require(tabs.CurrentPage is NovelSearchOptionsPage, "Advanced result type retains its original page and model binding");
                    expander.IsExpanded = false;
                    await RenderAsync(window);
                    await ActivateAsync(Chip(Pinned));
                    Require(routes.Count == 4 && Equals(routes[^1].WorkType, SimpleWorkType.Novel), "Collapsed ordinary search retains the selected result type");
                    container.CloseDestinationAndReturn();
                    window.Width = width == 360 ? 1000 : 360;
                    await RenderAsync(window);
                    Require(ReferenceEquals(input, page.FindControl<AutoCompleteBox>("SearchAutoCompleteBox")) && model.SearchText == Pinned,
                        "Resizing preserves the search editor and its draft");
                    HorizontalBounds(window, input);
                    Save(window, directory, "search-page-final-" + scenario);
                    report.Add($"PASS {scenario}: complete tags/translation, clipboard, pin/unpin/delete persistence, single search actions, trending/result type and resize");

                    SuggestionChip Chip(string value) => page.GetVisualDescendants().OfType<SuggestionChip>().Single(control =>
                        control.DataContext is string tag && tag == value || control.DataContext is SearchHistoryEntry entry && entry.Value == value);

                    async Task InvokeContextAsync(Control control, string resource)
                    {
                        await RevealAsync(control);
                        var menu = control.ContextMenu!;
                        menu.Open(control);
                        await RenderAsync(window);
                        var item = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, I18NManager.GetResource(resource)));
                        Require(item.Focus(NavigationMethod.Tab), "Tag context action is keyboard reachable");
                        Press(window, Key.Enter, PhysicalKey.Enter);
                        await RenderAsync(window);
                        Require(!menu.IsOpen, "Executing a tag context action closes its menu");
                    }

                    async Task ActivateAsync(Control control)
                    {
                        await RevealAsync(control);
                        Require(control.Focus(NavigationMethod.Tab), "Search action is keyboard reachable");
                        Press(window, Key.Space, PhysicalKey.Space);
                        await RenderAsync(window);
                    }

                    async Task RevealAsync(Control control)
                    {
                        control.BringIntoView();
                        await RenderAsync(window);
                        HorizontalBounds(window, control);
                    }
                }
                finally { window.Content = null; window.Close(); }
            }
        Require(fixture.UnexpectedRequests.IsEmpty, "No request escaped the offline search page fixture");
        File.WriteAllLines(Path.Combine(directory, "search-page-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {report.Count} search page groups.");
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(500); // Keep real navigation, expander and nested smooth-scroll animations.
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void HorizontalBounds(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window);
        Require(point is { } p && p.X >= -1 && p.X + control.Bounds.Width <= window.Bounds.Width + 1 && control.Bounds.Width > 0,
            $"{control.GetType().Name} stays within the {window.Width} DIP search page ({point}, {control.Bounds.Size})");
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Require(condition(), "Fixed trending data did not arrive");
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, "");
        window.KeyRelease(key, RawInputModifiers.None, physical, "");
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No search page frame.");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
