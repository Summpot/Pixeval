using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Mako;
using Mako.Net;
using Mako.Net.EndPoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using WebApiClientCore;

namespace Pixeval.MigrationValidation;

internal sealed class OfflineSearchFixture : IDisposable
{
    private readonly ConcurrentDictionary<string, Channel<PendingCompletion>> _requests = new();
    private readonly ConcurrentBag<PendingCompletion> _pending = [];
    internal ConcurrentQueue<string> UnexpectedRequests { get; } = new();
    internal ConcurrentQueue<string> RequestLog { get; } = new();
    internal ConcurrentQueue<Uri> SearchRequests { get; } = new();
    internal bool IsPremium { get; set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<string>>? AuthResponseFactory { get; set; }

    internal OfflineSearchFixture(MakoClient client)
    {
        var options = client.Provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();
        // Block every Mako HTTP client, including endpoints not expected in this scenario.
        // The App endpoint is internal. WebApiClientCore 2.1.6 names non-generic clients by full type name.
        foreach (var name in new[]
                 {
                     HttpApi.GetName(typeof(IAuthEndPoint)), "Mako.Net.EndPoints.IAppApiEndPoint",
                     nameof(MakoApiKind.AuthApi), nameof(MakoApiKind.AppApi), nameof(MakoApiKind.ImageApi), nameof(MakoApiKind.WebApi)
                 })
            options.Get(name).HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = new Handler(this));
    }

    internal Task<PendingCompletion> NextCompletionAsync(string keyword) =>
        GetRequests(keyword).Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));

    private Channel<PendingCompletion> GetRequests(string keyword) =>
        _requests.GetOrAdd(keyword, static _ => Channel.CreateUnbounded<PendingCompletion>());

    public void Dispose()
    {
        foreach (var pending in _pending)
            _ = pending.Response.TrySetResult("{\"tags\":[]}");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Missing request URI.");
        RequestLog.Enqueue($"{request.Method} {uri.PathAndQuery}");
        string json;
        switch (uri.AbsolutePath)
        {
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
                        "id":900001, "name":"M3E Fixture", "account":"fixture", "mail_address":"",
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
                json = "{\"trend_tags\":[]}";
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
