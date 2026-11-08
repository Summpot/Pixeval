using System;
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
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Config;
using Pixeval.Services.Background;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;

namespace Pixeval.Tests.Ui;

internal static class SeriesDetailsValidation
{
    private const long SeriesId = 994007;
    private const string ImageUri = "https://i.pximg.net/m3e/series-details.png";
    private const string Title = "长系列标题 / A long series title ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static readonly string Caption = string.Join('\n', Enumerable.Range(1, 20).Select(index => $"{index}: 系列介绍 / A long series description."));

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        client.SetRefreshToken("fixture-series-details");
        await FixtureAuthentication.IdentifyAsync(client);
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[ImageUri] = buffer.ToArray();
        }
        var details = Channel.CreateUnbounded<Pending>();
        var actions = Channel.CreateUnbounded<Pending>();
        List<Pending> pending = [];
        fixture.SeriesResponseFactory = async (request, token) =>
        {
            var operation = new Pending(request.RequestUri!, request.Content is null ? "" : await request.Content.ReadAsStringAsync(token), token);
            pending.Add(operation);
            await (request.RequestUri!.AbsolutePath.Contains("watchlist", StringComparison.Ordinal) ? actions : details).Writer.WriteAsync(operation);
            return await operation.Response.Task;
        };
        var subscriptions = App.AppViewModel.AppServiceProvider.GetRequiredService<IWorkSubscriptionService>();
        List<string> report = [];
        try
        {
            foreach (var type in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                    foreach (var width in new[] { 360, 1000 })
                    {
                        using var model = UiTestSession.Create<SeriesViewerPageViewModel>(type, SeriesId);
                        var page = UiTestSession.Create<SeriesViewerPage>(model);
                        var host = new PageTestHost { Content = page };
                        var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = host.Shell };
                        var key = $"{type}-{theme.Key}-{width}";
                        try
                        {
                            Pixeval.Common.MaterialAnimation.Initialize(window);
                            window.Show();
                            var initial = await NextAsync(details);
                            Require(model.IsLoading && !model.AddToWatchlistCommand.CanExecute(null), "Watchlist remains disabled until the series exists");
                            Require(initial.Uri.AbsolutePath == (type == SimpleWorkKind.Novel ? "/v2/novel/series" : "/v1/illust/series")
                                && HttpUtility.ParseQueryString(initial.Uri.Query)[type == SimpleWorkKind.Novel ? "series_id" : "illust_series_id"] == SeriesId.ToString(),
                                "Series loading keeps the original type-specific endpoint and ID");
                            initial.Response.SetResult(Response(type));
                            await UntilAsync(() => !model.IsLoading);
                            await RenderAsync(window);
                            Save(window, directory, "series-expanded-" + key);
                            Require(model.SeriesDetail?.Id == SeriesId && model.FirstWork is not null && model.LoadErrorMessage is null, "The fixed series loads successfully");
                            var title = page.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == Title && text.IsEffectivelyVisible);
                            var titlePosition = title.TranslatePoint(default, window)!.Value;
                            Require(titlePosition.X >= 0 && titlePosition.X + title.Bounds.Width <= width + 1 && title.Bounds.Width >= 140
                                && title.TextLayout.TextLines.All(line => !line.HasCollapsed), "The series title has readable space in a narrow profile");
                            var description = page.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == Caption);
                            var scroller = description.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
                            Require(scroller is not null && scroller.Extent.Height > scroller.Viewport.Height, "A long series caption remains scrollable");
                            Require(scroller!.Focus(NavigationMethod.Tab), "The series profile is keyboard scrollable");
                            for (var step = 0; step <= Math.Ceiling(scroller.Extent.Height / scroller.Viewport.Height); step++)
                                Press(window, Key.PageDown, PhysicalKey.PageDown);
                            await RenderAsync(window);
                            var last = description.TranslatePoint(new Point(0, description.Bounds.Height), scroller)!.Value;
                            Require(scroller.Offset.Y > 0 && last.Y > 0 && last.Y <= scroller.Bounds.Height + 1, "The final caption line is inside the viewport");
                            Save(window, directory, "series-caption-end-" + key);
                            var expander = page.FindControl<Expander>("SeriesInfoExpander")!;
                            expander.IsExpanded = false;
                            await RenderAsync(window);
                            var bar = expander.GetVisualDescendants().OfType<CommandBar>().Single();
                            var add = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, model.AddToWatchlistCommand));
                            await ActivateAsync(window, bar, add);
                            var operation = await NextAsync(actions);
                            Require(!model.RemoveFromWatchlistCommand.CanExecute(null), "Add and remove share one pending state");
                            await model.RemoveFromWatchlistCommand.ExecuteAsync(null);
                            Require(!actions.Reader.TryPeek(out _), "A direct competing command cannot start another watchlist request");
                            Require(operation.Uri.AbsolutePath == $"/v1/watchlist/{(type == SimpleWorkKind.Novel ? "novel" : "manga")}/add"
                                && HttpUtility.ParseQueryString(operation.Body)["series_id"] == SeriesId.ToString(), "Watchlist preserves its own endpoint and series ID");
                            operation.Response.SetResult("{}");
                            await UntilAsync(() => model.IsWatchlistAdded);
                            await RenderAsync(window);
                            Require(!add.IsVisible && subscriptions.TryGetSubscription(SeriesId, WorkSubscriptionType.Series,
                                    type == SimpleWorkKind.Novel ? WorkSubscriptionWorkKind.Novel : WorkSubscriptionWorkKind.Manga) is null,
                                "Server watchlist success never creates a local download subscription");
                            var remove = bar.PrimaryCommands.Concat(bar.SecondaryCommands).OfType<CommandBarButton>()
                                .SingleOrDefault(button => ReferenceEquals(button.Command, model.RemoveFromWatchlistCommand));
                            if (remove is null)
                                throw new InvalidOperationException("Missing remove: " + string.Join(" | ", bar.PrimaryCommands.Concat(bar.SecondaryCommands)
                                    .OfType<CommandBarButton>().Select(button => $"{button.Label}: visible={button.IsVisible}, command={button.Command}, context={button.DataContext?.GetType().Name}")));
                            await ActivateAsync(window, bar, remove);
                            operation = await NextAsync(actions);
                            operation.Response.SetException(new HttpRequestException("Fixed watchlist removal failure"));
                            await UntilAsync(() => !model.RemoveFromWatchlistCommand.IsRunning);
                            Require(model.IsWatchlistAdded && model.RemoveFromWatchlistCommand.CanExecute(null), "Failure preserves watchlist state and permits retry");
                            await ActivateAsync(window, bar, remove);
                            (await NextAsync(actions)).Response.SetResult("{}");
                            await UntilAsync(() => !model.IsWatchlistAdded);
                            await RenderAsync(window);
                            Save(window, directory, "series-collapsed-" + key);
                            var late = model.AddToWatchlistCommand.ExecuteAsync(null);
                            operation = await NextAsync(actions);
                            var source = page.GetLogicalDescendants().OfType<Control>().Select(control => control.DataContext)
                                .OfType<ISimpleViewViewModel>().First().Source;
                            Require(source.Count == 1, "The series owns a populated work list before closing");
                            host.Shell.CloseAllPages();
                            Require(operation.Token.IsCancellationRequested, "Closing the series page cancels watchlist transport");
                            operation.Response.SetResult("{}");
                            await late;
                            Require(!model.IsWatchlistAdded && !model.AddToWatchlistCommand.CanExecute(null)
                                && source.Count == 0 && page.FindControl<ContentControl>("SeriesContainerHost")!.Content is null,
                                "Closed series pages release their list and reject late watchlist state");
                            report.Add($"PASS {key}: detail identity, full profile/keyboard scroll, watchlist payload/failure/retry, no subscription side effect and close isolation");
                        }
                        finally
                        {
                            host.Shell.CloseAllPages();
                            host.Content = null;
                            window.Close();
                        }
                    }
            foreach (var type in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
            {
                using var failed = UiTestSession.Create<SeriesViewerPageViewModel>(type, SeriesId);
                (await NextAsync(details)).Response.SetException(new HttpRequestException("Fixed series detail failure"));
                await UntilAsync(() => !failed.IsLoading);
                Require(failed.SeriesDetail is null && failed.WorksEngine is null && !string.IsNullOrWhiteSpace(failed.LoadErrorMessage),
                    "Failed series details cannot publish an ID-zero record or engine");
                using var closed = UiTestSession.Create<SeriesViewerPageViewModel>(type, SeriesId);
                var initial = await NextAsync(details);
                closed.Dispose();
                await UntilAsync(() => initial.Token.IsCancellationRequested);
                Require(initial.Token.IsCancellationRequested, "Closing cancels pending series details");
                initial.Response.SetResult(Response(type));
                await Task.Delay(100);
                Dispatcher.UIThread.RunJobs();
                Require(closed.SeriesDetail is null && closed.WorksEngine is null, "Late series details never publish after close");
                report.Add($"PASS {type}-detail-lifetime: failed/default response and close cancellation");
            }
            using (var switched = UiTestSession.Create<SeriesViewerPageViewModel>(SimpleWorkKind.Novel, SeriesId))
            {
                (await NextAsync(details)).Response.SetResult(Response(SimpleWorkKind.Novel));
                await UntilAsync(() => !switched.IsLoading);
                var action = switched.AddToWatchlistCommand.ExecuteAsync(null);
                var operation = await NextAsync(actions);
                fixture.AuthResponseFactory = (_, _) => Task.FromResult(NativeFixtureData.Serialize(new TokenResponse { AccessToken = "fixture", RefreshToken = "fixture", TokenType = "Bearer", ExpiresIn = 3600 } with
                {
                    AccessToken = "fixture-second-access",
                    RefreshToken = "fixture-second-refresh",
                    ExpiresIn = 3600,
                    User = new TokenUser { Id = "", Name = "", Account = "" } with { Id = "900002", Name = "Fixture second account", Account = "fixture-second" }
                }));
                client.SetRefreshToken("fixture-second-account");
                await FixtureAuthentication.IdentifyAsync(client);
                Require(client.GetUser()?.Id == "900002", "The watchlist sample changes the isolated account");
                operation.Response.SetResult("{}");
                await action;
                Require(!switched.IsWatchlistAdded && !switched.SeriesDetail!.Value.WatchlistAdded, "The previous account cannot publish a late watchlist result");
                report.Add("PASS watchlist-account: late completion cannot change the next account's series state");
            }
            Require(fixture.UnexpectedRequests.IsEmpty, "Series detail requests remain inside the fixed endpoint matrix");
            File.WriteAllLines(Path.Combine(directory, "series-detail-contracts.txt"), report);
            Console.WriteLine($"Validated {report.Count} series-detail groups.");
        }
        finally
        {
            foreach (var operation in pending)
                operation.Response.TrySetCanceled();
            File.WriteAllLines(Path.Combine(directory, "series-detail-requests.txt"), fixture.RequestLog);
        }
    }

    private static string Response(SimpleWorkKind type)
    {
        var user = User.CreateDefault() with { Id = 900007, Name = "长作者 / A long series author", ProfileImageUrls = new() { Medium = ImageUri } };
        var image = Illustration.CreateDefault() with
        {
            Id = 994008,
            Title = "Fixture series work",
            User = user,
            Width = 640,
            Height = 480,
            PageCount = 1,
            ImageUrls = new() { Medium = ImageUri, SquareMedium = ImageUri, Large = ImageUri },
            MetaSinglePage = new() { OriginalImageUrl = ImageUri }
        };
        var novel = Mako.Novel.CreateDefault() with { Id = 994008, Title = image.Title, User = user, ImageUrls = image.ImageUrls };
        return type == SimpleWorkKind.Novel
            ? NativeFixtureData.Serialize(new { novel_series_detail = Series.CreateDefault() with { Id = SeriesId, Title = Title, MaskText = Caption, User = user, PublishedContentCount = 12345 }, novel_series_first_novel = novel, novel_series_latest_novel = novel, novels = new[] { novel }, next_url = (string?) null })
            : NativeFixtureData.Serialize(new { illust_series_detail = Series.CreateDefault() with { Id = SeriesId, Title = Title, MaskText = Caption, User = user, PublishedContentCount = 12345, CoverUrl = ImageUri }, illust_series_first_illust = image, illusts = new[] { image }, next_url = (string?) null });
    }
    private static Task<Pending> NextAsync(Channel<Pending> channel) => channel.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    private static async Task ActivateAsync(Window window, CommandBar bar, CommandBarButton button)
    {
        if (button.IsInOverflow && !bar.IsOpen)
        {
            Require(bar.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "PART_OverflowButton").Focus(NavigationMethod.Tab), "Series overflow is keyboard reachable");
            Press(window, Key.Enter, PhysicalKey.Enter);
            await RenderAsync(window);
            Require(bar.IsOpen, "The series overflow opens");
        }
        var point = button.TranslatePoint(default, window)!.Value;
        Require(point.X >= -1 && point.X + button.Bounds.Width <= window.ClientSize.Width + 1 && button.Bounds.Width > 0, "Watchlist actions fit the window");
        Require(button.Focus(NavigationMethod.Tab), "Watchlist actions are keyboard reachable");
        Press(window, Key.Enter, PhysicalKey.Enter);
        await RenderAsync(window);
        if (bar.IsOpen)
        {
            Press(window, Key.Escape, PhysicalKey.Escape);
            await RenderAsync(window);
        }
    }
    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The series operation did not reach its expected state");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }
    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(250);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }
    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No series detail frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
    private sealed record Pending(Uri Uri, string Body, CancellationToken Token)
    {
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
