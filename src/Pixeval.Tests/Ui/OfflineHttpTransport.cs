using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Mako;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Pixeval.Tests.Ui;

/// <summary>Installed during composition, before any cached HTTP client is resolved.</summary>
internal sealed class OfflineHttpTransport
{
    private static readonly ConditionalWeakTable<MakoClient, OfflineHttpTransport> Transports = new();
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Respond { get; set; }

    internal static OfflineHttpTransport For(MakoClient client) => Transports.GetValue(client, _ =>
        throw new InvalidOperationException("The application must install offline transport before creating pages."));

    internal static void Install(MakoClient client)
    {
        var transport = new OfflineHttpTransport();
        Transports.Add(client, transport);
        var options = client.Provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();
        foreach (var name in new[] { "Mako.Net.EndPoints.IAuthEndPoint", "Mako.Net.EndPoints.IAppApiEndPoint", "AuthApi", "AppApi", "ImageApi", "WebApi" })
            options.Get(name).HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = new Handler(transport));
    }

    private sealed class Handler(OfflineHttpTransport transport) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            transport.Respond?.Invoke(request, cancellationToken)
            ?? throw new InvalidOperationException($"No offline response registered for {request.Method} {request.RequestUri?.AbsolutePath}");
    }
}
