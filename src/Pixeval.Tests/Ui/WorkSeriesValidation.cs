using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Material3Expressive.Components;
using Pixeval.Localization;
using Pixeval.Controls;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;

namespace Pixeval.Tests.Ui;

internal static class WorkSeriesValidation
{
    private const long SeriesId = 998100;
    private const string Title = "完整系列标题 / A complete long series title ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string ImageUri = "https://i.pximg.net/m3e/work-series.png";

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        client.SetRefreshToken("fixture-work-series");
        await FixtureAuthentication.IdentifyAsync(client);
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[ImageUri] = buffer.ToArray();
        }
        var requests = Channel.CreateUnbounded<Pending>();
        ConcurrentBag<Pending> pending = [];
        ConcurrentQueue<(Uri Uri, CancellationToken Token)> destinations = [];
        fixture.SeriesResponseFactory = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath != "/v1/illust-series/illust")
            {
                destinations.Enqueue((request.RequestUri, token));
                await Task.Delay(Timeout.Infinite, token);
                return "{}";
            }
            var operation = new Pending(request.RequestUri!, token);
            pending.Add(operation);
            await requests.Writer.WriteAsync(operation);
            return await operation.Response.Task;
        };
        List<string> report = [];
        try
        {
            using (var source = new FixtureEntries<object>([Sample(998001), Sample(998002)]))
            using (var viewer = UiTestSession.Create<IllustrationViewerPageViewModel>(source.CloneSourceView(), 0, false))
            {
                var old = await NextAsync();
                Require(HttpUtility.ParseQueryString(old.Uri.Query)["illust_id"] == "998001", "Manga context uses the current illustration ID");
                Require(viewer.SeriesInfo is { Id: SeriesId, Previous: null, Next: null }, "The known series remains available before context arrives");
                viewer.CurrentWorkIndex = 1;
                var current = await NextAsync();
                await UntilAsync(() => old.Token.IsCancellationRequested);
                Require(old.Token.IsCancellationRequested, "Switching illustration cancels the old series-context HTTP request");
                Require(HttpUtility.ParseQueryString(current.Uri.Query)["illust_id"] == "998002", "The replacement context request uses the new work ID");
                current.Response.SetResult(NativeFixtureData.Serialize(Context(SeriesId, "Current series")));
                await UntilAsync(() => viewer.SeriesInfo?.Title == "Current series");
                old.Response.SetResult(NativeFixtureData.Serialize(Context(998999, "Stale series")));
                await FlushAsync();
                Require(viewer.SeriesInfo?.Title == "Current series", "A late previous context cannot replace the active series");
                viewer.CurrentWorkIndex = 0;
                var failed = await NextAsync();
                failed.Response.SetException(new HttpRequestException("Fixed series context failure"));
                await FlushAsync();
                Require(viewer.SeriesInfo is { Id: SeriesId, Title: Title, Previous: null, Next: null }, "Failed context retains the known series entry without inventing neighbors");
                viewer.CurrentWorkIndex = 1;
                var closing = await NextAsync();
                viewer.Dispose();
                await UntilAsync(() => closing.Token.IsCancellationRequested);
                Require(closing.Token.IsCancellationRequested, "Closing the viewer cancels the pending series-context request");
                closing.Response.SetResult(NativeFixtureData.Serialize(Context(998999, "Closed series")));
                await FlushAsync();
                Require(viewer.SeriesInfo?.Id == SeriesId && viewer.SeriesInfo.Next is null, "A closed viewer cannot adopt late neighbors");
                report.Add("PASS manga context: original IDs, fallback, switch/close HTTP cancellation, failure and late-result ownership");
            }

            var hidden = Context(SeriesId, Title) with
            {
                Context = new MangaSeriesContextInfo() with
                {
                    ContentOrder = 10,
                    PrevIllust = Sample(998010) with { Visible = false },
                    NextIllust = Sample(0)
                }
            };
            Require(WorkSeriesInfoViewModel.Create(hidden) is { Previous: null, Next: null }, "Unavailable or zero-ID manga neighbors are not actionable");
            var novelContent = new NovelContent { Text = "", CoverUrl = "" } with
            {
                SeriesId = SeriesId,
                SeriesTitle = Title,
                SeriesNavigation = new()
                {
                    PrevNovel = new NovelNavigation() with { Id = 998010, Viewable = true, Title = "Previous " + Title },
                    NextNovel = new NovelNavigation() with { Id = 998012, Viewable = true, Title = "Next " + Title }
                }
            };
            Require(WorkSeriesInfoViewModel.Create(novelContent with { SeriesId = null, SeriesTitle = "" }, new Series { Id = SeriesId, Title = Title }) is { Id: SeriesId, Title: Title }, "Mako.Novel metadata retains its known series fallback");
            Require(WorkSeriesInfoViewModel.Create(new NovelContent { Text = "", CoverUrl = "" }, null) is null
                && WorkSeriesInfoViewModel.Create((object?) null, SimpleWorkKind.Illustration) is null, "Non-series works do not expose an empty series entry");
            Require(WorkSeriesInfoViewModel.Create(novelContent with
            {
                SeriesNavigation = new() { PrevNovel = new NovelNavigation() with { Id = 998010, Viewable = false } }
            }, null) is { Previous: null, Next: null }, "Unavailable novel neighbors are hidden");
            report.Add("PASS factories: manga/novel available-neighbor gates, absent series and original fallback metadata");

            foreach (var type in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                    foreach (var width in new[] { 300, 360, 1000 })
                    {
                        var model = type == SimpleWorkKind.Illustration
                            ? WorkSeriesInfoViewModel.Create(Context(SeriesId, Title))
                            : WorkSeriesInfoViewModel.Create(novelContent, null)!;
                        var pane = new WorkSeriesInfoPane { DataContext = model };
                        List<(Type Type, long? SeriesId, SimpleWorkKind? WorkKind, string? ArtworkId)> routes = [];
                        var host = new PageTestHost
                        {
                            Content = pane,
                            NavigationObserver = target => routes.Add((target.GetType(),
                                (target.DataContext as SeriesViewerPageViewModel)?.Id,
                                (target.DataContext as SeriesViewerPageViewModel)?.WorkKind,
                                ((target.DataContext as IllustrationViewerPageViewModel)?.CurrentIllustration as IWorkEntry)?.Id.ToString()))
                        };
                        var window = new Window { Width = width, Height = 500, RequestedThemeVariant = theme, Content = host.Shell };
                        var key = $"{type}-{theme.Key}-{width}";
                        try
                        {
                            Pixeval.Common.MaterialAnimation.Initialize(window);
                            window.Show();
                            await RenderAsync(window);
                            var open = pane.FindControl<Card>("OpenSeriesButton")!;
                            var previous = pane.FindControl<Card>("PreviousButton")!;
                            var next = pane.FindControl<Card>("NextButton")!;
                            Require(pane.IsVisible && open.IsClickable && previous.IsClickable && next.IsClickable, "All original series destinations remain clickable");
                            Require(AutomationProperties.GetName(open) == Strings.EntryItemOpenSeries()
                                && AutomationProperties.GetName(previous) == Strings.EntryViewerPagePreviousSeriesWork()
                                && AutomationProperties.GetName(next) == Strings.EntryViewerPageNextSeriesWork()
                                && AutomationProperties.GetHelpText(open) == Title
                                && AutomationProperties.GetHelpText(previous) == model.Previous!.Title
                                && AutomationProperties.GetHelpText(next) == model.Next!.Title, "Series actions expose localized direction and complete titles");
                            foreach (var button in new[] { open, previous, next })
                            {
                                var origin = button.TranslatePoint(default, window)!.Value;
                                Require(button.Bounds.Width >= 48 && button.Bounds.Height >= 48 && origin.X >= 0
                                    && origin.X + button.Bounds.Width <= width + 1, "Series actions fit within the actual narrow pane");
                            }
                            if (model.PositionText is { Length: > 0 } position)
                            {
                                var label = open.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == position);
                                Require(label.TextLayout.Width <= label.Bounds.Width + 1 && label.TextLayout.TextLines.All(line => !line.HasCollapsed),
                                    "The full manga position remains readable beside a long series title");
                            }
                            Require(open.Focus(NavigationMethod.Tab), "The series entry accepts keyboard focus");
                            Press(window, Key.Tab, PhysicalKey.Tab);
                            Require(previous.IsFocused, "Tab reaches the previous work");
                            Press(window, Key.Tab, PhysicalKey.Tab);
                            Require(next.IsFocused, "Tab reaches the next work");
                            Press(window, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Shift);
                            Require(previous.IsFocused, "Shift+Tab returns to the previous work");
                            await ActivateAsync(window, open, Key.Enter, PhysicalKey.Enter);
                            Require(routes.Count == 1 && routes[0].Type == typeof(SeriesViewerPage)
                                && routes[0].SeriesId == SeriesId && routes[0].WorkKind == type, "Series navigation retains original ID and work type exactly once");
                            host.CloseDestinationAndReturn();
                            await ActivateAsync(window, previous, Key.Space, PhysicalKey.Space);
                            Require(routes.Count == 2 && routes[1].Type == (type == SimpleWorkKind.Novel ? typeof(NovelViewerPage) : typeof(IllustrationViewerPage)), "Previous work navigates once with Space");
                            if (type == SimpleWorkKind.Novel)
                                await UntilAsync(() => destinations.Any(d => d.Uri.AbsolutePath == "/v2/novel/detail" && System.Web.HttpUtility.ParseQueryString(d.Uri.Query)["novel_id"] == "998010"));
                            host.CloseDestinationAndReturn();
                            var target = next.TranslatePoint(new Point(next.Bounds.Width / 2, next.Bounds.Height / 2), window)!.Value;
                            window.MouseDown(target, MouseButton.Left);
                            window.MouseUp(target, MouseButton.Left);
                            await RenderAsync(window);
                            Require(routes.Count == 3 && routes[2].Type == routes[1].Type, "Next work navigates once with pointer input");
                            if (type == SimpleWorkKind.Novel)
                                await UntilAsync(() => destinations.Any(d => d.Uri.AbsolutePath == "/v2/novel/detail" && System.Web.HttpUtility.ParseQueryString(d.Uri.Query)["novel_id"] == "998012"));
                            host.CloseDestinationAndReturn();
                            if (type == SimpleWorkKind.Illustration)
                            {
                                Require(routes[1].ArtworkId == "998010"
                                    && routes[2].ArtworkId == "998012", "Cached manga neighbors retain their original work objects and IDs");
                            }
                            else
                            {
                                Require(destinations.Any(d => d.Uri.AbsolutePath == "/v2/novel/detail" && HttpUtility.ParseQueryString(d.Uri.Query)["novel_id"] == "998010")
                                    && destinations.Any(d => d.Uri.AbsolutePath == "/v2/novel/detail" && HttpUtility.ParseQueryString(d.Uri.Query)["novel_id"] == "998012"), "Mako.Novel neighbors retain their exact detail request IDs");
                            }
                            Save(window, directory, "series-navigation-" + key);
                            if (type == SimpleWorkKind.Illustration)
                            {
                                pane.DataContext = model with { Previous = new WorkSeriesNavigationViewModel(998020, "ID-only " + Title) };
                                await RenderAsync(window);
                                await ActivateAsync(window, previous, Key.Enter, PhysicalKey.Enter);
                                Require(routes.Count == 4 && destinations.Any(d => d.Uri.AbsolutePath == "/v1/illust/detail"
                                    && HttpUtility.ParseQueryString(d.Uri.Query)["illust_id"] == "998020"), "An ID-only manga neighbor retains its original detail fallback route");
                                host.CloseDestinationAndReturn();
                            }
                            pane.DataContext = model with { Previous = null, Next = null };
                            await RenderAsync(window);
                            Require(!previous.IsVisible && !next.IsVisible && open.IsVisible, "First/last or unavailable neighbors leave the series entry available");
                            pane.DataContext = null;
                            Require(!pane.IsVisible, "A reused non-series pane hides all series actions");
                            pane.DataContext = model;
                            await RenderAsync(window);
                            Require(pane.IsVisible && previous.IsVisible && next.IsVisible, "Rebinding restores the new series and neighbor actions");
                            report.Add("PASS " + key + ": Material surface, full titles, keyboard/pointer single routes, exact targets and reuse");
                        }
                        finally { host.Content = null; window.Close(); }
                    }
            fixture.CommentResponseFactory = (_, _) => Task.FromResult("{\"comments\":[],\"next_url\":null}");
            fixture.RelatedResponseFactory = (_, _) => Task.FromResult("{\"illusts\":[],\"next_url\":null}");
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360, 1000 })
                {
                    using var viewer = UiTestSession.Create<IllustrationViewerPageViewModel>((Sample(998030)), false);
                    var response = await NextAsync(998030);
                    response.Response.SetResult(NativeFixtureData.Serialize(Context(SeriesId, Title)));
                    await UntilAsync(() => viewer.SeriesInfo?.Next is not null);
                    var pane = new IllustrationViewerInfoPane { DataContext = viewer };
                    var host = new PageTestHost { Content = pane };
                    var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = host.Shell };
                    try
                    {
                        Pixeval.Common.MaterialAnimation.Initialize(window);
                        window.Show();
                        await RenderAsync(window);
                        var seriesPane = pane.GetVisualDescendants().OfType<WorkSeriesInfoPane>().Single();
                        var information = pane.FindControl<WorkInfoPane>("InfoPaneSection")!;
                        Require(ReferenceEquals(seriesPane.DataContext, viewer.SeriesInfo) && seriesPane.IsEffectivelyVisible
                            && seriesPane.Bounds.Height is > 80 and < 250 && information.Bounds.Height > 40,
                            "The real illustration pane docks the current series at its content height and retains space for work information");
                        Save(window, directory, $"series-integrated-{theme.Key}-{width}");
                        report.Add($"PASS integrated {theme.Key}-{width}: current series binding and information/series layout coexist");
                    }
                    finally { host.Content = null; window.Close(); }
                }
            Require(destinations.All(d => d.Token.IsCancellationRequested), "Immediately closed destinations cancel their direct detail requests");
            Require(fixture.UnexpectedRequests.IsEmpty, "Series navigation stays in the fixed HTTP matrix: " + string.Join(", ", fixture.UnexpectedRequests));
            File.WriteAllLines(Path.Combine(directory, "work-series-contracts.txt"), report);
            Console.WriteLine($"Validated {report.Count} work-series groups.");
        }
        finally
        {
            foreach (var request in pending) request.Response.TrySetCanceled();
            File.WriteAllLines(Path.Combine(directory, "work-series-requests.txt"), fixture.RequestLog);
        }
        return;
        async Task<Pending> NextAsync(long? workId = null)
        {
            while (true)
            {
                var request = await requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                if (!request.Token.IsCancellationRequested && (workId is null || HttpUtility.ParseQueryString(request.Uri.Query)["illust_id"] == workId.ToString())) return request;
                request.Response.TrySetResult("{}");
            }
        }
    }

    private static Illustration Sample(long id) => Illustration.CreateDefault() with
    {
        Id = id,
        Title = "Work " + id + " " + Title,
        Visible = true,
        Width = 100,
        Height = 100,
        PageCount = 1,
        User = User.CreateDefault() with { Id = 900001, Name = "Fixture author", ProfileImageUrls = new() { Medium = ImageUri } },
        ImageUrls = new() { Medium = ImageUri, Large = ImageUri, SquareMedium = ImageUri },
        MetaSinglePage = new() { OriginalImageUrl = ImageUri },
        Series = new Series { Id = SeriesId, Title = Title }
    };
    private static MangaSeriesContextResult Context(long id, string title) => new MangaSeriesContextResult() with
    {
        Series = Series.CreateDefault() with { Id = id, Title = title, PublishedContentCount = 12345 },
        Context = new MangaSeriesContextInfo() with
        {
            ContentOrder = 1234,
            PrevIllust = Sample(998010) with { Series = null },
            NextIllust = Sample(998012) with { Series = null }
        }
    };
    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
    }
    private static async Task ActivateAsync(Window window, Control control, Key key, PhysicalKey physical)
    {
        Require(control.Focus(NavigationMethod.Tab), "The series action is keyboard reachable");
        Press(window, key, physical);
        await RenderAsync(window);
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Series state did not settle");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }
    private static async Task FlushAsync() { await Task.Delay(200); Dispatcher.UIThread.RunJobs(); }
    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(120);
        window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }
    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No series frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    private static void Require(bool condition, string contract)
    {
        if (!condition) throw new InvalidOperationException(contract);
    }
    private sealed record Pending(Uri Uri, CancellationToken Token)
    {
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
