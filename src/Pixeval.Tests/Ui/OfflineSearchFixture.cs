using System;
using System.Collections.Concurrent;
using System.IO;
using Pixeval.Services.Core;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Pixeval.Mako;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Pixeval.Tests.Ui;

internal sealed class OfflineSearchFixture : IDisposable
{
    private readonly ConcurrentDictionary<string, Channel<PendingCompletion>> _requests = new();
    private readonly ConcurrentBag<PendingCompletion> _pending = [];
    internal ConcurrentQueue<string> UnexpectedRequests { get; } = new();
    internal ConcurrentQueue<string> RequestLog { get; } = new();
    internal ConcurrentQueue<Uri> SearchRequests { get; } = new();
    internal ConcurrentDictionary<string, byte[]> ImageResponses { get; } = new();
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? ImageResponseFactory { get; set; }
    internal bool IsPremium { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? AuthResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? BookmarkResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? BookmarkWorksResponseFactory { get; set; }
    internal Func<Uri, string>? TrendingResponseFactory { get; set; }
    internal Func<Uri, string>? RankingResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? ArtworkDetailResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? NovelTextResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? UserResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? SeriesResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? WorkFeedResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? SpotlightResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? WorkCardResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? RelatedResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? SearchResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? CommentResponseFactory { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? AnimationResponseFactory { get; set; }

    internal OfflineSearchFixture(MakoClient client)
    {
        _transport = OfflineHttpTransport.For(client);
        if (_transport.Respond is not null)
            throw new InvalidOperationException("Only one HTTP fixture may own an application session.");
        _transport.Respond = SendAsync;
    }

    private readonly OfflineHttpTransport _transport;

    internal Task<PendingCompletion> NextCompletionAsync(string keyword) =>
        GetRequests(keyword).Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));

    private Channel<PendingCompletion> GetRequests(string keyword) =>
        _requests.GetOrAdd(keyword, static _ => Channel.CreateUnbounded<PendingCompletion>());

    public void Dispose()
    {
        _transport.Respond = null;
        var evidenceDirectory = Path.GetDirectoryName(AppInfo.ApplicationFolderPath)!;
        File.WriteAllLines(Path.Combine(evidenceDirectory, "offline-requests.txt"), RequestLog);
        File.WriteAllLines(Path.Combine(evidenceDirectory, "unexpected-requests.txt"), UnexpectedRequests);
        foreach (var pending in _pending)
            _ = pending.Response.TrySetResult("{\"tags\":[]}");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Missing request URI.");
        RequestLog.Enqueue($"{request.Method} {uri.PathAndQuery}");
        if (ImageResponses.TryGetValue(uri.AbsoluteUri, out var image))
            return new(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(image) };
        if (uri.Host == "i.pximg.net" && ImageResponseFactory is { } imageResponse)
            return await imageResponse(request, token).ConfigureAwait(false);
        if (BookmarkResponseFactory is { } bookmark && uri.AbsolutePath is
            "/v2/illust/bookmark/detail" or "/v2/novel/bookmark/detail" or "/v1/user/bookmark-tags/illust" or "/v1/user/bookmark-tags/novel")
            return new(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(await bookmark(request, token).ConfigureAwait(false), Encoding.UTF8, "application/json")
            };
        string json;
        switch (uri.AbsolutePath)
        {
            case "/v1/ugoira/metadata" when AnimationResponseFactory is { } animation:
                json = await animation(request, token).ConfigureAwait(false);
                break;
            case "/v1/user/ai-show-settings":
                json = "{\"show_ai\":true}";
                break;
            case "/v1/novel/text" when NovelTextResponseFactory is { } novelText:
                json = await novelText(request, token).ConfigureAwait(false);
                break;
            case "/v2/novel/markers" when NovelTextResponseFactory is not null:
                json = "{\"novels\":[],\"next_url\":null}";
                break;
            case "/v1/novel/marker/add" or "/v1/novel/marker/delete" when NovelTextResponseFactory is not null:
                json = "{}";
                break;
            case "/v1/illust/comment/add" or "/v1/novel/comment/add" or "/v3/illust/comments" or "/v3/novel/comments"
                or "/v2/illust/comment/replies" or "/v2/novel/comment/replies"
                or "/v1/illust/comment/delete" or "/v1/novel/comment/delete" or "/m3e/comments-next" when CommentResponseFactory is { } comment:
                json = await comment(request, token).ConfigureAwait(false);
                break;
            case "/v1/search/popular-preview/illust" or "/v1/search/popular-preview/novel":
                json = "{\"illusts\":[],\"novels\":[],\"next_url\":null}";
                break;
            case "/v1/search/illust" or "/v1/search/novel" when SearchResponseFactory is { } search:
                SearchRequests.Enqueue(uri);
                json = await search(request, token).ConfigureAwait(false);
                break;
            case "/v2/illust/related" or "/v1/novel/related" or "/m3e/related-next" when RelatedResponseFactory is { } related:
                json = await related(request, token).ConfigureAwait(false);
                break;
            case "/v2/illust/bookmark/add" or "/v2/novel/bookmark/add" or "/v1/illust/bookmark/delete"
                or "/v1/novel/bookmark/delete" or "/webview/v2/novel" when WorkCardResponseFactory is { } card:
                json = await card(request, token).ConfigureAwait(false);
                break;
            case "/v1/spotlight/articles" or "/m3e/spotlight-next" when SpotlightResponseFactory is { } spotlight:
                json = await spotlight(request, token).ConfigureAwait(false);
                break;
            case "/v2/illust/follow" or "/v1/novel/follow" or "/v2/illust/mypixiv" or "/v2/novel/mypixiv"
                or "/v1/illust/recommended" or "/v1/novel/recommended" or "/v1/illust/new" or "/v1/novel/new"
                or "/v1/user/illusts" or "/v1/user/novels" or "/m3e/feed-next" when WorkFeedResponseFactory is { } feed:
                json = await feed(request, token).ConfigureAwait(false);
                break;
            case "/v1/watchlist/manga" or "/v1/watchlist/novel" or "/m3e/series-next"
                or "/v1/watchlist/manga/add" or "/v1/watchlist/manga/delete" or "/v1/watchlist/novel/add" or "/v1/watchlist/novel/delete"
                or "/v1/illust/series" or "/v2/novel/series" or "/v1/illust/detail" or "/v2/novel/detail"
                or "/v2/user/detail" or "/v1/illust-series/illust" when SeriesResponseFactory is { } series:
                json = await series(request, token).ConfigureAwait(false);
                break;
            case "/v1/user/recommended" or "/v1/search/user" or "/v1/user/following" or "/v1/user/follower"
                or "/v1/user/mypixiv" or "/v1/user/related" or "/v2/user/detail" or "/v1/user/follow/add"
                or "/v1/user/follow/delete" or "/m3e/user-next" when UserResponseFactory is { } users:
                json = await users(request, token).ConfigureAwait(false);
                break;
            case "/v1/illust/detail" or "/v2/novel/detail" when ArtworkDetailResponseFactory is { } detail:
                json = await detail(request, token).ConfigureAwait(false);
                break;
            case "/v1/illust/ranking" or "/v1/novel/ranking" when RankingResponseFactory is { } rankings:
                json = rankings(uri);
                break;
            case "/v1/user/bookmarks/illust" or "/v1/user/bookmarks/novel" when BookmarkWorksResponseFactory is { } works:
                json = await works(request, token).ConfigureAwait(false);
                break;
            case "/auth/token":
                if (AuthResponseFactory is { } respond)
                {
                    json = await respond(request, token).ConfigureAwait(false);
                    break;
                }
                json = $$"""
                    {
                      "access_token":"fixture-access", "refresh_token":"fixture-refresh",
                      "expires_in":3600, "token_type":"bearer", "scope":"",
                      "user": {
                        "id":"900001", "name":"M3E Fixture", "account":"fixture", "mail_address":"",
                        "is_premium":{{(IsPremium ? "true" : "false")}}, "x_restrict":0, "is_mail_authorized":true,
                        "require_policy_agreement":false,
                        "profile_image_urls":{"px_16x16":"", "px_50x50":"", "px_170x170":""}
                      }
                    }
                    """;
                break;
            case "/v1/search/illust":
                SearchRequests.Enqueue(uri);
                json = "{\"illusts\":[],\"next_url\":null}";
                break;
            case "/v1/search/novel":
                SearchRequests.Enqueue(uri);
                json = "{\"novels\":[],\"next_url\":null}";
                break;
            case "/v1/trending-tags/illust":
            case "/v1/trending-tags/novel":
                json = TrendingResponseFactory?.Invoke(uri) ?? "{\"trend_tags\":[]}";
                break;
            case "/v1/search/options":
                json = """
                    {
                      "illust":{"bookmark_ranges":[],"show_ai_condition":true,"lang":{"options":[]},"tool":{"options":["Fixture tool"]}},
                      "novel":{"bookmark_ranges":[],"show_ai_condition":true,"lang":{"options":[{"code":"ja","name":"日本語"}]},"genre":{"options":[{"id":1,"label":"Fixture genre"}]},"word_count_supported_languages":"en"}
                    }
                    """;
                break;
            case "/v2/search/autocomplete":
                var keyword = HttpUtility.ParseQueryString(uri.Query)["word"]
                    ?? throw new InvalidOperationException("Autocomplete omitted its keyword.");
                var pending = new PendingCompletion(token);
                _pending.Add(pending);
                if (!GetRequests(keyword).Writer.TryWrite(pending))
                    throw new InvalidOperationException("Could not record autocomplete request.");
                // Intentionally finish even after cancellation to exercise late responses.
                json = await pending.Response.Task.ConfigureAwait(false);
                break;
            default:
                UnexpectedRequests.Enqueue($"{request.Method} {uri.PathAndQuery}");
                throw new InvalidOperationException($"Unexpected offline request: {request.Method} {uri.AbsolutePath}");
        }
        return new(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    internal sealed class PendingCompletion(CancellationToken token)
    {
        internal CancellationToken Token { get; } = token;
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Complete(string name) => Response.SetResult(
            $$"""{"tags":[{"name":"{{JsonEncodedText.Encode(name)}}","translated_name":"Fixture translation"}]}""");

        internal void Fail() => Response.SetException(new HttpRequestException("Fixed autocomplete connection failure"));
    }

    private sealed class Handler(OfflineSearchFixture fixture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => fixture.SendAsync(request, cancellationToken);
    }
}
