using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Pixeval.Common.Collections;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views;
using Pixeval.Views.Works;
using Pixeval.Views.Viewers;

namespace Pixeval.Tests.Ui;

internal static class SeriesPageValidation
{
    private const string LongTitle = "长系列标题 / A very long series title with several words ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        var requests = Channel.CreateUnbounded<PendingSeries>();
        ConcurrentBag<PendingSeries> pending = [];
        ConcurrentQueue<(Uri Uri, CancellationToken Token)> details = [];
        fixture.SeriesResponseFactory = async (request, token) =>
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath is "/v1/watchlist/manga" or "/v1/watchlist/novel" or "/m3e/series-next")
            {
                var response = new PendingSeries(uri);
                pending.Add(response);
                await requests.Writer.WriteAsync(response);
                return await response.Response.Task;
            }
            details.Enqueue((uri, token));
            await Task.Delay(Timeout.Infinite, token); // Destinations are observed and disposed without displaying a second page.
            return "{}";
        };
        const string imageUri = "https://i.pximg.net/m3e/series.png";
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[imageUri] = buffer.ToArray();
        }
        client.SetRefreshToken("fixture-series-page");
        await FixtureAuthentication.IdentifyAsync(client);
        var originalType = App.AppViewModel.AppSettings.SearchSettings.DefaultSimpleWorkType;
        List<string> report = [];
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360, 1000 })
                    foreach (var workType in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
                    {
                        App.AppViewModel.AppSettings.SearchSettings.DefaultSimpleWorkType = workType;
                        var page = UiTestSession.Create<SeriesPage>();
                        var view = page.FindControl<SeriesView>("SeriesView")!;
                        var model = (SeriesViewViewModel) view.DataContext!;
                        List<(Type Page, long Id, SimpleWorkKind? WorkKind)> routes = [];
                        var container = new PageTestHost
                        {
                            Content = page,
                            NavigationObserver = destination => routes.Add(destination.DataContext switch
                            {
                                SeriesViewerPageViewModel series => (destination.GetType(), series.Id, series.WorkKind),
                                UserViewerPageViewModel user => (destination.GetType(), user.Id, null),
                                _ => (destination.GetType(), 0, null)
                            })
                        };
                        var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
                        var key = $"{workType}-{theme.Key}-{width}";
                        try
                        {
                            Pixeval.Common.MaterialAnimation.Initialize(window);
                            window.Show();
                            var first = await NextAsync();
                            Require(first.Uri.AbsolutePath == PathFor(workType), "The watchlist uses the selected manga/novel endpoint");
                            first.Complete([Sample(930001), Sample(930002)]);
                            await UntilAsync(() => model.Source.Count == 2 && !Source().IsLoading);
                            await RenderAsync(window);
                            var card = Card(930001);
                            Save(window, directory, "series-before-" + key);
                            await ActivateAsync(window, card, Key.Enter, PhysicalKey.Enter);
                            Require(routes.Count == 1 && routes[0] == (typeof(SeriesViewerPage), 930001L, workType), "Enter opens exactly one series detail with its ID and work type");
                            container.CloseDestinationAndReturn();
                            await ActivateAsync(window, card, Key.Space, PhysicalKey.Space);
                        container.CloseDestinationAndReturn();
                            await ClickAsync(window, card, new Point(20, 25));
                            Require(routes.Count == 3 && routes.All(route => route == routes[0]), "Space and pointer use the same single series navigation");
                            container.CloseDestinationAndReturn();
                            Require(AutomationProperties.GetName(card) == LongTitle && Equals(ToolTip.GetTip(card), LongTitle), "The full series title remains accessible outside its truncated preview");
                            var author = card.FindControl<Button>("AuthorButton")!;
                            var latest = card.FindControl<Button>("LatestContentButton")!;
                            card.Focus(NavigationMethod.Tab);
                            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                            Require(author.IsFocused, "Tab from the series card reaches the author action; focused=" + window.FocusManager?.GetFocusedElement()
                                + "; list mode=" + KeyboardNavigation.GetTabNavigation(view.FindControl<ListBox>("SeriesListBox")!));
                            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                            Require(latest.IsFocused, "The latest-content action follows the author in keyboard traversal");
                            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
                            window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
                            Require(author.IsFocused, "Shift+Tab reverses the series action order");
                            Bounds(window, card, card);
                            Bounds(window, author, card);
                            Bounds(window, latest, card);
                            var count = routes.Count;
                            await ActivateAsync(window, author, Key.Space, PhysicalKey.Space);
                            Require(routes.Count == count + 1 && routes[^1] == (typeof(UserViewerPage), 900007L, null), "Author activation opens only the author, without also opening the series");
                            container.CloseDestinationAndReturn();
                            await ClickAsync(window, author);
                            Require(routes.Count == count + 2 && routes[^1].Page == typeof(UserViewerPage), "Pointer author activation remains independent of the outer card");
                            container.CloseDestinationAndReturn();
                            await ActivateAsync(window, latest, Key.Enter, PhysicalKey.Enter);
                            Require(routes.Count == count + 3 && routes[^1].Page == LatestPage(workType), "Latest-content keyboard action opens only the correct work viewer");
                            container.CloseDestinationAndReturn();
                            Require(details.Any(request => request.Uri.AbsolutePath == DetailPath(workType)
                                && HttpUtility.ParseQueryString(request.Uri.Query)[workType == SimpleWorkKind.Novel ? "novel_id" : "illust_id"] == "940001"),
                                "The latest-content request preserves its work ID");
                            await ClickAsync(window, latest);
                            Require(routes.Count == count + 4 && routes[^1].Page == LatestPage(workType), "Pointer latest-content action does not also open series detail");
                            container.CloseDestinationAndReturn();

                            var menu = card.ContextMenu ?? ((Grid) card.Content!).ContextMenu!;
                            card.Focus(NavigationMethod.Tab);
                            window.KeyPress(Key.Apps, RawInputModifiers.None, PhysicalKey.ContextMenu, null);
                            window.KeyRelease(Key.Apps, RawInputModifiers.None, PhysicalKey.ContextMenu, null);
                            await RenderAsync(window);
                            Require(menu.IsOpen, "A focused series card exposes its actions through the platform context-menu key");
                            menu.Close();
                            await OpenContextAsync();
                            var entries = menu.Items.OfType<MenuItem>().ToArray();
                            Require(Equals(entries.Single(item => Equals(item.Header, Strings.EntryItemOpenInWebBrowser())).Tag,
                                ((Series) card.DataContext!).WebsiteUri), "Open in browser retains the original type-specific series URL");
                            var copy = entries.Single(item => Equals(item.Header, Strings.EntryItemGenerateWebLink()));
                            await ActivateAsync(window, copy, Key.Enter, PhysicalKey.Enter);
                            Require(await window.Clipboard!.TryGetTextAsync() == ((Series) card.DataContext!).WebsiteUri.ToString(), "Series website copy preserves the type-specific URL");
                            await OpenContextAsync();
                            await ActivateAsync(window, entries.Single(item => Equals(item.Header, Strings.EntryItemGenerateLink())), Key.Enter, PhysicalKey.Enter);
                            Require(await window.Clipboard!.TryGetTextAsync() == ((Series) card.DataContext!).AppUri.ToString(), "Series app-link copy preserves work type and series ID");
                            await OpenContextAsync();
                            count = routes.Count;
                            await ActivateAsync(window, entries.Single(item => Equals(item.Header, Strings.SeriesPageViewLatestContent())), Key.Enter, PhysicalKey.Enter);
                            Require(routes.Count == count + 1 && routes[^1].Page == LatestPage(workType), "The context-menu latest action uses the same single destination");
                            container.CloseDestinationAndReturn();
                            await OpenContextAsync();
                            await ActivateAsync(window, entries.Single(item => Equals(item.Header, Strings.EntryItemOpenUserInfoPage())), Key.Enter, PhysicalKey.Enter);
                            Require(routes.Count == count + 2 && routes[^1] == (typeof(UserViewerPage), 900007L, null), "The context-menu author action retains its ID without opening the series");
                            container.CloseDestinationAndReturn();
                            await OpenContextAsync();
                            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                            await RenderAsync(window);
                            Require(!menu.IsOpen, "Escape closes the series context menu");

                            var refresh = page.FindControl<CommandBarButton>("RefreshButton")!;
                            Require(refresh.Label == Strings.WorkContainerRefreshButtonLabel(), "Series refresh exposes its localized label");
                            await ActivateAsync(window, refresh, Key.Space, PhysicalKey.Space);
                            var refreshed = await NextAsync();
                            Require(refreshed.Uri.AbsolutePath == PathFor(workType), "Refresh retains the selected work type");
                            refreshed.Complete([Sample(930003)], "https://app-api.pixiv.net/m3e/series-next?offset=1");
                            var next = await NextAsync();
                            Require(next.Uri.AbsolutePath == "/m3e/series-next" && HttpUtility.ParseQueryString(next.Uri.Query)["offset"] == "1", "Series pagination uses the exact service next_url");
                            next.Complete([Sample(930003), Sample(930004)]);
                            await UntilAsync(() => model.Source.Count == 2 && !Source().IsLoading);
                            Require(model.Source.Select(item => item.Id).SequenceEqual(new long[] { 930003, 930004 }), "Series pagination preserves order and de-duplicates IDs");

                            var selector = page.FindControl<SymbolComboBox>("SimpleWorkTypeComboBox")!.GetVisualDescendants().OfType<ComboBox>().Single();
                            Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(selector)), "The series work-type selector is named");
                            await ActivateAsync(window, selector, workType == SimpleWorkKind.Illustration ? Key.Down : Key.Up,
                                workType == SimpleWorkKind.Illustration ? PhysicalKey.ArrowDown : PhysicalKey.ArrowUp);
                            var switched = await NextAsync();
                            Require(switched.Uri.AbsolutePath != PathFor(workType), "Keyboard selection switches between manga and novel watchlists");
                            selector.SelectedIndex = workType == SimpleWorkKind.Illustration ? 0 : 1;
                            switched.Complete([Sample(939999)], "https://app-api.pixiv.net/m3e/obsolete-series-next");
                            var restored = await NextAsync();
                            Require(restored.Uri.AbsolutePath == PathFor(workType), "Restoring the selector restores its original endpoint");
                            restored.Complete([]);
                            await UntilAsync(() => !Source().IsLoading);
                            Require(model.Source.Count == 0 && !requests.Reader.TryPeek(out _), "The obsolete source cannot publish its entry or follow next_url");
                            Save(window, directory, "series-empty-" + key);
                            await ActivateAsync(window, refresh, Key.Space, PhysicalKey.Space);
                            var late = await NextAsync();
                            var closedSource = model.Source;
                            container.Shell.CloseAllPages();
                            late.Complete([Sample(939998)], "https://app-api.pixiv.net/m3e/obsolete-series-next");
                            await RenderAsync(window);
                            Require(closedSource.Count == 0 && !requests.Reader.TryPeek(out _), "Closing a loading series list rejects late data and pagination");
                            report.Add($"PASS {key}: exact sources, keyboard/pointer child routing, copy/context, refresh, pagination, empty/type-switch and disposal");

                            async Task OpenContextAsync()
                            {
                                await ClickAsync(window, card, new Point(20, 25), MouseButton.Right);
                                Require(menu.IsOpen, "The existing series context menu remains reachable with a right click");
                            }
                            SeriesItem Card(long id) => view.GetVisualDescendants().OfType<SeriesItem>().Single(item => item.DataContext is Series row && row.Id == id);
                            IncrementalLoadingCollection<Series> Source() => (IncrementalLoadingCollection<Series>) model.Source;
                        }
                        finally
                        {
                            container.Shell.CloseAllPages();
                            window.Close();
                        }
                    }
            Require(fixture.UnexpectedRequests.IsEmpty, "All series requests remain in the offline endpoint matrix");
            Require(details.All(request => request.Token.IsCancellationRequested), "All immediately closed destinations cancel their pending detail requests: "
                + string.Join(", ", details.Where(request => !request.Token.IsCancellationRequested).Select(request => request.Uri.PathAndQuery)));
            File.WriteAllLines(Path.Combine(directory, "series-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "series-requests.txt"), fixture.RequestLog);
            Console.WriteLine($"Validated {report.Count} series-page groups.");
        }
        finally
        {
            App.AppViewModel.AppSettings.SearchSettings.DefaultSimpleWorkType = originalType;
            foreach (var request in pending)
                _ = request.Response.TrySetResult("{\"series\":[],\"next_url\":null}");
        }
        Task<PendingSeries> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Series Sample(long id) => Series.CreateDefault() with
        {
            Id = id, Title = LongTitle, CoverUrl = imageUri,
            User = User.CreateDefault() with { Id = 900007, Name = "长作者名 / A very long author name" },
            PublishedContentCount = 1234, LatestContentId = id + 10000,
            LastPublishedContentDatetime = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero).ToString("O")
        };
    }

    private static string PathFor(SimpleWorkKind type) => type == SimpleWorkKind.Novel ? "/v1/watchlist/novel" : "/v1/watchlist/manga";
    private static string DetailPath(SimpleWorkKind type) => type == SimpleWorkKind.Novel ? "/v2/novel/detail" : "/v1/illust/detail";
    private static Type LatestPage(SimpleWorkKind type) => type == SimpleWorkKind.Novel ? typeof(NovelViewerPage) : typeof(IllustrationViewerPage);

    private static async Task ActivateAsync(Window window, Control control, Key key, PhysicalKey physical)
    {
        control.BringIntoView();
        await RenderAsync(window);
        Require(control.Focus(NavigationMethod.Tab), "The series action is keyboard reachable");
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
        await RenderAsync(window);
    }

    private static async Task ClickAsync(Window window, Control control, Point? offset = null, MouseButton button = MouseButton.Left)
    {
        control.BringIntoView();
        await RenderAsync(window);
        var point = control.TranslatePoint(offset ?? new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, button);
        window.MouseUp(point, button);
        await RenderAsync(window);
    }

    private static void Bounds(Window window, Control control, Control card)
    {
        var origin = control.TranslatePoint(default, window)!.Value;
        var local = control.TranslatePoint(default, card)!.Value;
        Require(control.Bounds.Width > 0 && control.Bounds.Height > 0 && origin.X >= -1 && origin.X + control.Bounds.Width <= window.ClientSize.Width + 1
            && local.Y >= -1 && local.Y + control.Bounds.Height <= card.Bounds.Height + 1, "Card content and actions fit inside the card and horizontal viewport");
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(350);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The series operation did not reach its expected state.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No series frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }

    private sealed class PendingSeries(Uri uri)
    {
        internal Uri Uri { get; } = uri;
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(IReadOnlyList<Series> series, string? next = null) =>
            Response.SetResult(NativeFixtureData.Serialize(new { series, next_url = next }));
    }
}
