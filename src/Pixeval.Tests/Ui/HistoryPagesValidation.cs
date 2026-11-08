using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Services.Core;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Storage;
using Pixeval.Config;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views;
using Pixeval.Views.Viewers;
using Pixeval.Views.Works;

namespace Pixeval.Tests.Ui;

internal static class HistoryPagesValidation
{
    internal static async Task RunAsync(string directory)
    {
        var app = App.AppViewModel;
        using var fixture = new OfflineSearchFixture(app.MakoClient);
        app.MakoClient.SetRefreshToken("fixture-history-pages");
        await FixtureAuthentication.IdentifyAsync(app.MakoClient);
        fixture.WorkCardResponseFactory = (_, _) => throw new HttpRequestException("Fixed unavailable novel body; only the destination identity is inspected.");
        fixture.SeriesResponseFactory = async (_, token) =>
        {
            await Task.Delay(-1, token);
            return "{}";
        };
        var helper = app.StorageEngine;
        var browse = app.StorageEngine.HistoryRepository;
        var watch = app.StorageEngine.WatchLaterRepository;
        List<string> report = [];
        foreach (var isWatchLater in new[] { false, true })
            foreach (var initialType in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                    foreach (var width in new[] { 360, 1000 })
                    {
                        browse.Clear();
                        watch.Clear();
                        foreach (var type in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
                            for (var index = 1; index <= 3; index++)
                                Add(Create(type, index));
                        if (isWatchLater)
                            browse.AddBrowseHistory(Create(SimpleWorkKind.Illustration, 99));
                        else
                            watch.AddWatchLater(Create(SimpleWorkKind.Illustration, 99));
                        var key = $"{(isWatchLater ? "watch-later" : "browse")}-{initialType}-{theme.Key}-{width}";
                        app.AppSettings.SearchSettings.DefaultSimpleWorkType = initialType;
                        HistoryPage page = isWatchLater ? UiTestSession.Create<WatchLaterPage>() : UiTestSession.Create<BrowsingHistoryPage>();
                        var works = page.FindControl<WorkContainer>("WorkContainer")!;
                        var view = works.FindControl<WorkView>("WorkView")!;
                        var list = view.FindControl<WorkListBox>("WorkListBox")!;
                        var field = page.FindControl<SymbolComboBox>("SimpleWorkTypeComboBox")!;
                        List<(Type Page, string? Id)> routes = [];
                        var host = new PageTestHost
                        {
                            Content = page,
                            NavigationObserver = destination => routes.Add((destination.GetType(), destination.DataContext switch
                            {
                                IllustrationViewerPageViewModel illustration => (illustration.CurrentIllustration as IWorkEntry)?.Id.ToString(),
                                NovelViewerPageViewModel novel => novel.NovelId.ToString(),
                                _ => null
                            }))
                        };
                        var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = host.Shell };
                        try
                        {
                            Pixeval.Common.MaterialAnimation.Initialize(window);
                            window.Show();
                            await UntilAsync(() => Model().Source.Count == 3 && !Loading());
                            await RenderAsync(window);
                            Require(Model().Source.Select(item => ((IWorkEntry)item).Id.ToString()).SequenceEqual(new[] { "990003", "990002", "990001" }),
                                "History retains newest-record-first order without mixing identical IDs from different work types");
                            Require(Model().RequireAdaptiveGrid == (initialType == SimpleWorkKind.Novel), "History retains its typed work layout");
                            var combo = field.GetVisualDescendants().OfType<ComboBox>().Single();
                            Require(!field.IconMode && AutomationProperties.GetName(combo) == Strings.HomePageSourceWorkTypeTextBlockText(),
                                "The history type field has a readable selected value and localized purpose name");
                            var origin = field.TranslatePoint(default, window)!.Value;
                            Require(origin.X >= -1 && origin.X + field.Bounds.Width <= window.ClientSize.Width + 1, "The history type fits within the narrow toolbar");
                            foreach (var text in combo.GetVisualDescendants().OfType<TextBlock>().Where(text => !string.IsNullOrEmpty(text.Text)))
                                Require(text.TextLayout.TextLines.All(line => !line.HasCollapsed) && text.TextLayout.Width <= text.Bounds.Width + 1,
                                    "Long-language history type labels remain readable");
                            Save(window, directory, "history-ready-" + key);
                            works.FindControl<SymbolComboBox>("SortOptionComboBox")!.SelectedValue = LocalSortOption.PublishDateAscending;
                            await RenderAsync(window);
                            Require(Model().View.Select(item => ((IWorkEntry)item).Id.ToString()).SequenceEqual(new[] { "990001", "990002", "990003" }),
                                "History uses the existing local date sort without rewriting record order");
                            works.FindControl<SymbolComboBox>("SortOptionComboBox")!.SelectedValue = LocalSortOption.DoNotSort;
                            var oldSource = Model().Source;
                            Require(combo.Focus(NavigationMethod.Tab), "The history type selector receives keyboard focus");
                            Press(window, initialType == SimpleWorkKind.Illustration ? Key.Down : Key.Up,
                                initialType == SimpleWorkKind.Illustration ? PhysicalKey.ArrowDown : PhysicalKey.ArrowUp);
                            var currentType = initialType == SimpleWorkKind.Illustration ? SimpleWorkKind.Novel : SimpleWorkKind.Illustration;
                            await UntilAsync(() => Model().Source.Count == 3 && !Loading());
                            Require(oldSource.Count == 0 && Model().RequireAdaptiveGrid == (currentType == SimpleWorkKind.Novel),
                                "Keyboard type switching releases the previous model and retains the new type's own history");
                            await Task.Run(() => Add(Create(currentType, 1, "Updated oldest record")));
                            await UntilAsync(() => (Model().Source.FirstOrDefault() as IWorkEntry)?.Title == "Updated oldest record" && !Loading());
                            Require(Model().Source.Count == 3 && ((IWorkEntry)Model().Source.First()).Id.ToString() == "990001", "Revisiting an existing work updates its payload and recency without duplicating it");

                            using (var reopenedDb = new Pixeval.Storage.StorageEngine(AppInfo.DatabaseFilePath))
                            {
                                var logger = app.AppServiceProvider.GetRequiredService<FileLogger>();
                                IArtworkHistorySource reopened = isWatchLater
                                    ? reopenedDb.WatchLaterRepository
                                    : reopenedDb.HistoryRepository;
                                var restored = await reopened.StreamAsync(currentType).ToArrayAsync();
                                Require(restored.Cast<IWorkEntry>().Select(entry => entry.Id.ToString()).SequenceEqual(new[] { "990001", "990003", "990002" })
                                    && ((IWorkEntry)restored[0]).Title == "Updated oldest record", "Reopening the existing SQLite file retains work identity, type, recency and payload");
                            }

                            await RefreshAsync(window, works);
                            await UntilAsync(() => Model().Source.Count == 3 && !Loading());
                            await RenderAsync(window);
                            var card = view.GetVisualDescendants().OfType<WorkItem>().First(control => ((IWorkEntry) control.DataContext!).Id == 990001);
                            Require(card.Focus(NavigationMethod.Tab), "The history card is keyboard reachable");
                            Press(window, Key.Enter, PhysicalKey.Enter);
                            await RenderAsync(window);
                            Require(routes.Count == 1 && routes[0] == (currentType == SimpleWorkKind.Novel ? typeof(NovelViewerPage) : typeof(IllustrationViewerPage), "990001"),
                                "Opening a history record preserves its original work viewer and ID exactly once");
                            host.CloseDestinationAndReturn();
                            if (isWatchLater)
                            {
                                card.Focus(NavigationMethod.Tab);
                                Press(window, Key.Apps, PhysicalKey.ContextMenu);
                                await RenderAsync(window);
                                var action = card.ContextMenu!.Items.OfType<MenuItem>().Single(item => Equals(item.Header, Strings.EntryItemAddToWatchLater()));
                                Require(action.Focus(NavigationMethod.Tab), "Watch-later removal is reachable in the existing card menu");
                                Press(window, Key.Enter, PhysicalKey.Enter);
                                await UntilAsync(() => Model().Source.Count == 2 && !Loading());
                                Require(!watch.ContainsWatchLater(Create(currentType, 1)) && Model().Source.All(item => ((IWorkEntry)item).Id.ToString() != "990001") && routes.Count == 1,
                                    "The card's watch-later toggle removes the database record and refreshes its page without navigation");
                            }
                            else
                            {
                                _ = Pixeval.Storage.WatchLaterRecord.TryCreateWorkKey(Create(currentType, 1), out var workKey);
                                Require(browse.TryDeleteBrowseHistoryByWorkKey(workKey), "The original history deletion API finds the same work identity");
                                await UntilAsync(() => Model().Source.Count == 2 && !Loading());
                            }
                            var otherHistoryCount = isWatchLater ? browse.CountBrowseHistory() : watch.CountWatchLater();
                            _ = Pixeval.Storage.WatchLaterRecord.TryCreateWorkKey(Create(SimpleWorkKind.Illustration, 99), out var sentinelKey);
                            if (isWatchLater)
                                watch.Clear();
                            else
                                browse.Clear();
                            await UntilAsync(() => Model().Source.Count == 0 && !Loading());
                            await RenderAsync(window);
                            Require((isWatchLater ? browse.CountBrowseHistory() : watch.CountWatchLater()) == otherHistoryCount
                                && (isWatchLater ? (object?)browse.GetBrowseHistoryByWorkKey(sentinelKey) : watch.GetWatchLaterByWorkKey(sentinelKey)) is not null,
                                "Clearing one history kind leaves the other kind and its sentinel record intact");
                            Save(window, directory, "history-empty-" + key);
                            await RefreshAsync(window, works);
                            await UntilAsync(() => !Loading());
                            Require(Model().Source.Count == 0, "An empty history can still refresh");
                            var closedSource = Model().Source;
                            Add(Create(currentType, 7)); // Queues Source.Changed before the owner closes.
                            page.Dispose();
                            host.Shell.CloseAllPages();
                            Add(Create(currentType, 8)); // Must no longer be subscribed.
                            await RenderAsync(window);
                            Require(closedSource.Count == 0 && view.DataContext is null && list.Items.Count == 0,
                                "Queued and subsequent history changes cannot revive a closed page");
                            report.Add($"PASS {key}: named type, keyboard switch, sort, recency, SQLite reopen, navigation, removal/clear and disposal");
                        }
                        catch
                        {
                            Save(window, directory, "history-failure-" + key);
                            throw;
                        }
                        finally
                        {
                            page.Dispose();
                            host.Shell.CloseAllPages();
                            window.Close();
                        }

                        IWorkViewViewModel Model() => (IWorkViewViewModel) view.DataContext!;
                        bool Loading() => list.GetValue(IncrementalLoadingBehavior.IsLoadingMoreProperty);
                        void Add(IWorkEntry entry)
                        {
                            if (isWatchLater)
                                watch.AddWatchLater(entry);
                            else
                                browse.AddBrowseHistory(entry);
                        }
                    }
        await ValidatePagingAsync(directory, report);
        Require(fixture.UnexpectedRequests.IsEmpty, "History requests stay inside its fixed navigation fixture");
        File.WriteAllLines(Path.Combine(directory, "history-page-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "history-page-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {report.Count} history-page groups.");
    }

    private static async Task ValidatePagingAsync(string directory, List<string> report)
    {
        var app = App.AppViewModel;
        app.StorageEngine.HistoryRepository.ClearBrowseHistory();
        app.AppSettings.SearchSettings.DefaultSimpleWorkType = SimpleWorkKind.Illustration;
        for (var index = 1; index <= 125; index++)
            app.StorageEngine.HistoryRepository.AddBrowseHistory(Create(SimpleWorkKind.Illustration, index));
        var page = UiTestSession.Create<BrowsingHistoryPage>();
        var view = page.FindControl<WorkContainer>("WorkContainer")!.FindControl<WorkView>("WorkView")!;
        var list = view.FindControl<WorkListBox>("WorkListBox")!;
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1000, Height = 720, Content = host.Shell };
        try
        {
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            var model = (IWorkViewViewModel) view.DataContext!;
            await UntilAsync(() => model.Source.Count >= 20 && !list.GetValue(IncrementalLoadingBehavior.IsLoadingMoreProperty));
            for (var pageIndex = 0; pageIndex < 10 && model.Source.Count < 125; pageIndex++)
            {
                var previousCount = model.Source.Count;
                list.ScrollIntoView(previousCount - 1);
                await RenderAsync(window);
                await UntilAsync(() => model.Source.Count > previousCount && !list.GetValue(IncrementalLoadingBehavior.IsLoadingMoreProperty));
            }
            Require(model.Source.Select(item => ((IWorkEntry)item).Id.ToString()).SequenceEqual(Enumerable.Range(1, 125).Reverse().Select(index => (990000 + index).ToString())),
                "Scrolling crosses the SQLite 100-record page boundary without lost, duplicated or reordered records");
            list.ScrollIntoView(124);
            await RenderAsync(window);
            Require(list.ContainerFromIndex(124) is ListBoxItem { DataContext: IWorkEntry { Id: 990001 } }
                && view.GetVisualDescendants().OfType<WorkItem>().Count() < 125,
                "The oldest record remains reachable while history retains virtualized containers");
            Save(window, directory, "history-paging-oldest");
            report.Add("PASS paging: 125 records across SQLite pages, newest-first identity, actual incremental scroll and virtualized oldest entry");
        }
        finally
        {
            page.Dispose();
            view.Dispose();
            window.Close();
        }
    }

    private static IWorkEntry Create(SimpleWorkKind type, int index, string? title = null) => type == SimpleWorkKind.Novel
        ? Mako.Novel.CreateDefault() with { Id = 990000 + index, Title = title ?? "历史小说 / History novel " + index, User = User.CreateDefault() with { Id = 900007, Name = "Fixture author" }, CreateDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(index).ToString("O") }
        : Illustration.CreateDefault() with { Id = 990000 + index, Title = title ?? "历史作品 / History artwork " + index, User = User.CreateDefault() with { Id = 900007, Name = "Fixture author" }, CreateDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(index).ToString("O"), Width = 640, Height = 480, PageCount = 1 };

    private static async Task RefreshAsync(Window window, WorkContainer works)
    {
        works.FindControl<CommandBar>("RightToolBar")!.IsOpen = true;
        await RenderAsync(window);
        Require(works.FindControl<CommandBarButton>("RefreshButton")!.Focus(NavigationMethod.Tab), "History refresh is keyboard reachable");
        Press(window, Key.Enter, PhysicalKey.Enter);
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(200);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("History did not reach the expected state.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No history frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
