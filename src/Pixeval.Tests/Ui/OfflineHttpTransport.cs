using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Cache;
using Pixeval.Maho;
using Pixeval.Mako;

namespace Pixeval.Tests.Ui;

// The fixture replaces transport only. Production Rust constructs and parses every request.
internal sealed class OfflineHttpTransport : IMahoTransport
{
    private static readonly ConditionalWeakTable<MakoClient, OfflineHttpTransport> Transports = new();
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _requests = new();
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Respond { get; set; }

    internal static OfflineHttpTransport For(MakoClient client) => Transports.GetValue(client, _ =>
        throw new InvalidOperationException("The application must install offline transport before creating pages."));

    internal static void Install(MakoClient client, MahoClient maho, ImageCache images)
    {
        var transport = new OfflineHttpTransport();
        Transports.Add(client, transport);
        client.SetTransport(transport);
        maho.SetTransport(transport);
        images.SetTransport(transport);
    }

    public async Task<MahoResponseData> SendAsync(ulong requestId, string method, string url,
        Dictionary<string, string> headers, byte[] body)
    {
        using var cancellation = new CancellationTokenSource();
        _requests[requestId] = cancellation;
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (body.Length > 0) request.Content = new ByteArrayContent(body);
            foreach (var (name, value) in headers)
                if (!request.Headers.TryAddWithoutValidation(name, value))
                    request.Content?.Headers.TryAddWithoutValidation(name, value);
            using var response = await (Respond?.Invoke(request, cancellation.Token)
                ?? throw new InvalidOperationException($"No offline response registered for {method} {request.RequestUri?.AbsolutePath}"));
            var responseHeaders = response.Headers.Concat(response.Content.Headers)
                .ToDictionary(pair => pair.Key, pair => string.Join(",", pair.Value));
            return new((ushort)response.StatusCode, responseHeaders, await response.Content.ReadAsByteArrayAsync());
        }
        catch (Exception error)
        {
            throw new MahoError.Network(error.Message);
        }
        finally { _requests.TryRemove(requestId, out _); }
    }

    public void Cancel(ulong requestId)
    {
        if (_requests.TryGetValue(requestId, out var cancellation))
            try { cancellation.Cancel(); } catch (ObjectDisposedException) { }
    }
}
