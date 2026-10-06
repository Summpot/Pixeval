using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Utilities;
using Pixeval.Utilities.IO;

namespace Pixeval.Tests;

[TestClass]
public sealed class IoHelperDownloadTest
{
    [TestMethod]
    public async Task PreviewCanReadGrowingBufferWithoutCorruptingDownload()
    {
        var expected = new byte[16384];
        new Random(42).NextBytes(expected);
        using var client = new HttpClient(new ImageResponseHandler(expected));
        using var destination = new MemoryStream();
        var updates = 0;
        var error = await client.DownloadStreamAsync(
            destination,
            new Uri("https://example.test/image"),
            bufferSize: 1024,
            onDataAvailable: (stream, _) =>
            {
                updates++;
                stream.Position = 0;
                Assert.AreEqual(expected[0], stream.ReadByte());
                return Task.CompletedTask;
            });

        Assert.IsNull(error);
        Assert.IsTrue(updates > 1);
        CollectionAssert.AreEqual(expected, destination.ToArray());
    }

    [TestMethod]
    public async Task CancellationDuringPreviewReleasesDownloadBuffer()
    {
        using var client = new HttpClient(new ImageResponseHandler(new byte[16384]));
        using var cancellation = new CancellationTokenSource();
        using var destination = new MemoryStream();
        Stream? borrowed = null;
        var error = await client.DownloadStreamAsync(
            destination,
            new Uri("https://example.test/image"),
            onDataAvailable: (stream, token) =>
            {
                borrowed = stream;
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            token: cancellation.Token);

        Assert.IsNotNull(error);
        Assert.IsNotNull(borrowed);
    }

    private sealed class ImageResponseHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }

    [TestMethod]
    public async Task InvalidUriShouldReturnFailure()
    {
        using var client = new HttpClient();

        var result = await client.DownloadByteArrayAsync("http://[");

        Assert.IsInstanceOfType<Result<Memory<byte>>.Failure>(result);
    }

    [TestMethod]
    public async Task LocalFileUriShouldCopyToDestinationStream()
    {
        var path = Path.GetTempFileName();
        var expected = new byte[] { 4, 5, 6 };
        await File.WriteAllBytesAsync(path, expected);

        try
        {
            using var client = new HttpClient();
            var uri = new UriBuilder(Uri.UriSchemeFile, "", -1, path).Uri;
            using var destination = new MemoryStream();
            var error = await client.DownloadStreamAsync(destination, uri);

            Assert.IsNull(error);
            CollectionAssert.AreEqual(expected, destination.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task PixivImageRequestShouldInjectRefererAndUserAgent()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = new HttpClient(new HeaderInspectHandler(req => capturedRequest = req));
        using var destination = new MemoryStream();

        var error = await client.DownloadStreamAsync(destination, new Uri("https://i.pximg.net/c/240x480/custom.jpg"));

        Assert.IsNull(error);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(new Uri("https://app-api.pixiv.net/"), capturedRequest.Headers.Referrer);
        Assert.IsTrue(capturedRequest.Headers.UserAgent.ToString().Contains("PixivAndroidApp"));
    }

    [TestMethod]
    public async Task NonPixivRequestShouldNotInjectPixivHeaders()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = new HttpClient(new HeaderInspectHandler(req => capturedRequest = req));
        using var destination = new MemoryStream();

        var error = await client.DownloadStreamAsync(destination, new Uri("https://example.com/image.png"));

        Assert.IsNull(error);
        Assert.IsNotNull(capturedRequest);
        Assert.IsNull(capturedRequest.Headers.Referrer);
    }

    private sealed class HeaderInspectHandler(Action<HttpRequestMessage> inspect) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            inspect(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
            });
        }
    }
}
