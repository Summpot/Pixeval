using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Config;
using Pixeval.Services.Update;

namespace Pixeval.Tests;

[TestClass]
public sealed class GitHubServiceTest
{
    [TestMethod]
    public async Task ResetAndDisposeLetInFlightNativeRequestsComplete()
    {
        var settings = new NetworkSettingsGroup();
        settings.ProxySettings.ProxyType = ProxyType.None;
        settings.GitHubDomainFronting.EnableGitHubDomainFronting = false;
        using var service = new GitHubService(settings);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
        var first = service.DownloadBytesAsync(url, timeout.Token);
        using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
        var stream = connection.GetStream();
        var request = new byte[4096];
        Assert.IsTrue(await stream.ReadAsync(request, timeout.Token) > 0);

        service.Reset();
        service.Dispose();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"), timeout.Token);
        Assert.AreEqual("ok", Encoding.ASCII.GetString(await first));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => service.DownloadBytesAsync(url));
    }

    [TestMethod]
    public async Task NativeRequestCanBeCancelledWhileWaitingForResponse()
    {
        var settings = new NetworkSettingsGroup();
        settings.ProxySettings.ProxyType = ProxyType.None;
        settings.GitHubDomainFronting.EnableGitHubDomainFronting = false;
        using var service = new GitHubService(settings);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var cancellation = new CancellationTokenSource();
        var task = service.DownloadBytesAsync($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/", cancellation.Token);
        using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => task.WaitAsync(timeout.Token));
    }
}
