using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Pixeval.Common.Collections;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.ViewModels;
using Pixeval.Views.Works;

namespace Pixeval.Tests.Ui;

internal static class SauceNaoResultsValidation
{
    internal static async Task RunAsync(string directory, byte[] image, List<string> report, OfflineSearchFixture fixture)
    {
        var client = App.AppViewModel.MakoClient;
        ConcurrentQueue<long> detailRequests = [];
        var delayedDetails = Channel.CreateUnbounded<(CancellationToken Token, TaskCompletionSource<string> Response)>();
        const string imageUri = "https://i.pximg.net/m3e/saucenao.png";
        fixture.ImageResponses[imageUri] = image;
        fixture.ArtworkDetailResponseFactory = async (request, token) =>
        {
            var id = long.Parse(HttpUtility.ParseQueryString(request.RequestUri!.Query)["illust_id"]!);
            detailRequests.Enqueue(id);
            if (id == 910006)
            {
                var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                await delayedDetails.Writer.WriteAsync((token, response));
                return await response.Task;
            }
            if (id == 910008)
                throw new HttpRequestException("Fixed unavailable artwork");
            return Artwork(id);
        };

        string Artwork(long id) => NativeFixtureData.Serialize(new
            {
                illust = Illustration.CreateDefault() with
                {
                    Id = id, Title = "SauceNao fixed artwork / 长标题", Width = 128, Height = 128,
                    ImageUrls = new() { Medium = imageUri, Large = imageUri, SquareMedium = imageUri }
                }
            });
        client.SetRefreshToken("fixture-saucenao-results");
        await FixtureAuthentication.IdentifyAsync(client);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
            {
                using var handler = new ResponseHandler(image);
                using var http = new HttpClient(handler);
                using var page = new ArtworkSauceNaoSearchResultPage("fixture-sauce-key", image, new OfflineHttpTransport { Respond = http.SendAsync });
                var navigation = new NavigationPage();
                await navigation.PushAsync(page);
                var container = new PageTestHost { Content = navigation };
                var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
                var works = page.FindControl<WorkContainer>("WorkContainer")!;
                var failure = page.FindControl<StackPanel>("FailurePanel")!;
                var error = page.FindControl<InfoBar>("SearchError")!;
                var retry = page.FindControl<Button>("RetryButton")!;
                var scenario = $"{theme.Key}-{width}";
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(window);
                    window.Show();
                    var limited = await handler.NextAsync();
                    Require(Source().IsLoading && !failure.IsVisible, "The real result collection stays loading until the HTTP request completes");
                    limited.Complete(HttpStatusCode.TooManyRequests, "{\"message\":\"fixed rate limit\"}");
                    await UntilAsync(() => failure.IsVisible && !Source().IsLoading);
                    Require(error.Text == Strings.SearchSauceNAORequestFailed(),
                        "HTTP rate limits are recoverable errors rather than a successful empty result");
                    Require(!works.IsVisible, "A failed search does not simultaneously display the successful empty-list state");
                    Require(limited.Content!.Disposed, "The failed HTTP response content is disposed");
                    await RenderAsync(window);
                    Save(window, directory, "saucenao-rate-limit-" + scenario);

                    await RetryAsync();
                    var rejected = await handler.NextAsync();
                    const string serviceMessage = "Fixed SauceNao quota exhausted. 长服务消息用于验证换行。 Please wait before retrying this search; the original image must stay available and no API credentials may appear in this page.";
                    rejected.Complete(HttpStatusCode.OK, NativeFixtureData.Serialize(new { header = new { user_id = 1, status = -1, message = serviceMessage }, results = Array.Empty<object>() }));
                    await UntilAsync(() => failure.IsVisible && !Source().IsLoading);
                    Require(error.Text == serviceMessage, "Service-status errors retain their supplied explanation");
                    await RenderAsync(window);
                    foreach (var text in error.GetVisualDescendants().OfType<TextBlock>())
                        Require(text.TextLayout.Width <= text.Bounds.Width + 1 && text.TextLayout.Height <= text.Bounds.Height + 1,
                            "The complete service error wraps within the result page");
                    Save(window, directory, "saucenao-service-error-" + scenario);

                    await RetryAsync();
                    var invalid = await handler.NextAsync();
                    invalid.Complete(HttpStatusCode.OK, "invalid-json");
                    await UntilAsync(() => failure.IsVisible && !Source().IsLoading);
                    Require(!error.Text!.Contains("fixture-sauce-key", StringComparison.Ordinal), "Malformed responses never expose the query API key");
                    await RetryAsync();
                    var success = await handler.NextAsync();
                    detailRequests.Clear();
                    success.Complete(HttpStatusCode.OK, Success(910001, 910002));
                    await UntilAsync(() => Source().Count == 2 && !Source().IsLoading);
                    Require(!failure.IsVisible && Source().Select(entry => ((Illustration)entry).Id.ToString()).SequenceEqual(new[] { "910001", "910002" }),
                        "Successful retry populates the existing work list with supported artwork identities in response order");
                    Require(detailRequests.SequenceEqual(new long[] { 910001, 910002 }), "An unsupported SauceNao result is skipped without an artwork request");
                    Require(success.Content!.Disposed, "The successful HTTP response is disposed after deserialization");
                    await RenderAsync(window);
                    Save(window, directory, "saucenao-results-" + scenario);

                    await page.PostAsync("fixture-sauce-key", image);
                    var obsolete = await handler.NextAsync();
                    await page.PostAsync("fixture-sauce-key", image);
                    await UntilAsync(() => obsolete.Token.IsCancellationRequested);
                    Require(obsolete.Token.IsCancellationRequested, "Replacing a result source propagates cancellation into its HTTP request");
                    // IncrementalLoadingBehavior serializes load calls even across source changes.
                    // Release this deliberately cancellation-ignoring transport before the next load.
                    obsolete.Fail();
                    var current = await handler.NextAsync();
                    Require(!failure.IsVisible, "An obsolete request failure is not shown while the replacement loads");
                    current.Complete(HttpStatusCode.OK, Success(910003));
                    await UntilAsync(() => Source().Count == 1 && !Source().IsLoading);
                    await RenderAsync(window);
                    Require(!failure.IsVisible && ((Illustration)Source().Single()).Id == 910003, "An obsolete request failure cannot replace current results or show an error");

                    await page.PostAsync("fixture-sauce-key", image);
                    var detached = await handler.NextAsync();
                    container.Content = null;
                    await RenderAsync(window);
                    await UntilAsync(() => detached.Token.IsCancellationRequested);
                    Require(detached.Token.IsCancellationRequested, "Leaving a loading result page cancels its HTTP operation");
                    var detailsBefore = detailRequests.Count;
                    detached.Complete(HttpStatusCode.OK, Success(910004));
                    await RenderAsync(window);
                    Require(detailRequests.Count == detailsBefore && !failure.IsVisible, "Detached late results do not start artwork requests or publish errors");
                    Require(detached.Content!.Disposed, "A late canceled HTTP response releases its content");
                    container.Content = navigation;
                    await RenderAsync(window);
                    var resumed = await handler.NextAsync();
                    resumed.Complete(HttpStatusCode.OK, Success());
                    await UntilAsync(() => !Source().IsLoading);
                    Require(Source().Count == 0 && !failure.IsVisible, "Returning to an interrupted page retries once and can display a successful empty result");
                    await RenderAsync(window);
                    Save(window, directory, "saucenao-empty-" + scenario);

                    var callsBeforeQueuedLoad = handler.Calls;
                    await page.PostAsync("fixture-sauce-key", image);
                    container.Content = null;
                    await RenderAsync(window);
                    Require(handler.Calls == callsBeforeQueuedLoad, "A queued result load does not start while its page is detached");
                    container.Content = navigation;
                    await RenderAsync(window);
                    var queuedResume = await handler.NextAsync();
                    queuedResume.Complete(HttpStatusCode.OK, Success());
                    await UntilAsync(() => !Source().IsLoading);
                    Require(!failure.IsVisible, "Reattaching also restarts a source canceled before its first incremental callback");

                    await page.PostAsync("fixture-sauce-key", image);
                    var resolving = await handler.NextAsync();
                    resolving.Complete(HttpStatusCode.OK, Success(910006, 910007));
                    var delayedDetail = await delayedDetails.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                    container.Content = null;
                    await RenderAsync(window);
                    await UntilAsync(() => delayedDetail.Token.IsCancellationRequested);
                    Require(delayedDetail.Token.IsCancellationRequested, "Leaving the page also cancels the platform artwork-resolution request");
                    detailsBefore = detailRequests.Count;
                    delayedDetail.Response.SetResult(Artwork(910006));
                    await RenderAsync(window);
                    Require(detailRequests.Count == detailsBefore && !failure.IsVisible, "A canceled artwork lookup cannot continue resolving later matches");
                    container.Content = navigation;
                    await RenderAsync(window);
                    var partial = await handler.NextAsync();
                    partial.Complete(HttpStatusCode.OK, Success(910008, 910009));
                    await UntilAsync(() => Source().Count == 1 && !Source().IsLoading);
                    Require(((Illustration)Source().Single()).Id == 910009 && !failure.IsVisible,
                        "An unavailable artwork keeps the existing skip-and-continue behavior for later matches");

                    await page.PostAsync("fixture-sauce-key", image);
                    var closed = await handler.NextAsync();
                    detailsBefore = detailRequests.Count;
                    container.Shell.CloseAllPages();
                    await UntilAsync(() => closed.Token.IsCancellationRequested);
                    Require(closed.Token.IsCancellationRequested, "Closing the page through the production shell cancels the outstanding request");
                    closed.Complete(HttpStatusCode.OK, Success(910005));
                    await RenderAsync(window);
                    Require(detailRequests.Count == detailsBefore && !failure.IsVisible, "A closed result page cannot resolve late artwork or report an error");
                    Require(closed.Content!.Disposed, "A closed page releases a late response body");
                    report.Add($"PASS results {scenario}: multipart bytes/key/options, loading, HTTP/service/JSON errors, explicit retry, artwork order, response disposal, replace/detach/close cancellation and empty results");
                }
                finally
                {
                    container.Shell.CloseAllPages();
                    window.Close();
                }

                IncrementalLoadingCollection<object> Source() =>
                    (IncrementalLoadingCollection<object>) ((IllustrationViewViewModel) works.DataContext!).Source;

                async Task RetryAsync()
                {
                    retry.BringIntoView();
                    await RenderAsync(window);
                    Require(retry.Focus(NavigationMethod.Tab), "Retry is keyboard reachable");
                    var calls = handler.Calls;
                    window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                    window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                    await UntilAsync(() => handler.Calls > calls);
                    Require(handler.Calls == calls + 1 && !failure.IsVisible, "One retry starts one new request and clears the previous error");
                }
            }
        Require(fixture.UnexpectedRequests.IsEmpty, "All artwork and image requests stayed inside the offline fixture");
        File.WriteAllLines(Path.Combine(directory, "saucenao-artwork-requests.txt"), fixture.RequestLog);
    }

    private static string Success(params long[] ids)
    {
        List<object?> results =
        [
            null,
            new { header = new { similarity = "99.0", index_id = 0 }, data = new { unsupported_id = 1 } },
            new { header = new { similarity = "99.0", index_id = 0 }, data = (object?) null }
        ];
        results.AddRange(ids.Select(id => new { header = new { similarity = "97.5", index_id = 5 }, data = new { pixiv_id = id } }));
        return NativeFixtureData.Serialize(new { header = new { user_id = 1, status = 0 }, results });
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(500);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("SauceNao results did not reach the expected state.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No SauceNao results frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }

    private sealed class ResponseHandler(byte[] image) : HttpMessageHandler
    {
        private readonly Channel<PendingResponse> _requests = Channel.CreateUnbounded<PendingResponse>();
        private readonly ConcurrentBag<PendingResponse> _pending = [];
        internal int Calls { get; private set; }
        internal Task<PendingResponse> NextAsync() => _requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Require(request.Method == HttpMethod.Post && request.RequestUri is { Host: "saucenao.com", AbsolutePath: "/search.php" },
                "Image search uses its original SauceNao POST endpoint");
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            Require(query["api_key"] == "fixture-sauce-key" && query["output_type"] == "2" && query["db"] == "999",
                "The original key, JSON format and all-index request values are preserved");
            var body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            Require(request.Content.Headers.ContentType?.MediaType == "multipart/form-data"
                && System.Text.Encoding.UTF8.GetString(body).Contains("name=\"file\"; filename=\"image.jpg\"")
                && body.AsSpan().IndexOf(image) >= 0, "The Rust multipart upload retains complete image bytes and file metadata");
            var pending = new PendingResponse(cancellationToken);
            _pending.Add(pending);
            Calls++;
            await _requests.Writer.WriteAsync(pending, cancellationToken);
            // Ignore cancellation deliberately: the production continuation must still reject a late response.
            return await pending.Response.Task;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var pending in _pending)
                    _ = pending.Response.TrySetCanceled();
            base.Dispose(disposing);
        }
    }

    private sealed class PendingResponse(CancellationToken token)
    {
        internal CancellationToken Token { get; } = token;
        internal TaskCompletionSource<HttpResponseMessage> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TrackingContent? Content { get; private set; }
        internal void Complete(HttpStatusCode status, string json)
        {
            Content = new(json);
            Response.SetResult(new(status) { Content = Content });
        }
        internal void Fail() => Response.SetException(new HttpRequestException("fixed obsolete request failure"));
    }

    private sealed class TrackingContent(string value) : StringContent(value, Encoding.UTF8, "application/json")
    {
        internal bool Disposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Disposed = true;
            base.Dispose(disposing);
        }
    }
}
