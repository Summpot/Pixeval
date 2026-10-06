// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Options;
using Pixeval.Native.Maho;

namespace Pixeval.Utilities.Network;

/// <summary>
/// 提供基于 Rust pixeval_maho 原生 DnsResolver 与 TLS 报文分片的 SocketsHttpHandler。
/// </summary>
public static class MahoSocketsHttpHandlerFactory
{
    public static SocketsHttpHandler CreateHandler(DnsResolver dnsResolver, int splitDelayMs = 100) =>
        CreateHandler(dnsResolver, splitDelayMs, networkSettings: null);

    public static SocketsHttpHandler CreateHandler(
        DnsResolver dnsResolver,
        int splitDelayMs = 100,
        NetworkSettingsGroup? networkSettings = null)
    {
        ArgumentNullException.ThrowIfNull(dnsResolver);

        var hasProxy = networkSettings?.ProxySettings.ProxyType is ProxyType.System or ProxyType.Custom;
        var proxy = hasProxy && networkSettings is not null
            ? PixivDirectProxy.Create(networkSettings)
            : null;

        return new SocketsHttpHandler
        {
            UseProxy = proxy is not null,
            Proxy = proxy,
            ConnectCallback = async (ctx, cancellationToken) =>
            {
                var host = ctx.DnsEndPoint.Host;
                var port = ctx.DnsEndPoint.Port;

                // 如果是代理服务器连接或非 Pixiv 直连目标，使用标准 TCP Socket 直连
                if (!IsPixivHost(host))
                {
                    var proxySocket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };
                    try
                    {
                        await proxySocket.ConnectAsync(ctx.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(proxySocket, ownsSocket: true);
                    }
                    catch
                    {
                        proxySocket.Dispose();
                        throw;
                    }
                }

                // Pixiv 域名：通过 DnsResolver 进行抗审查解析并经由 MahoTlsFragmentedStream 进行 TLS ClientHello 分片
                var resolvedIps = await dnsResolver.ResolveAsync(host).ConfigureAwait(false);
                if (resolvedIps.Count == 0)
                {
                    throw new SocketException((int) SocketError.HostNotFound);
                }

                Exception? lastException = null;
                foreach (var ipStr in resolvedIps)
                {
                    if (!IPAddress.TryParse(ipStr, out var ip))
                        continue;

                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(ip, port), cancellationToken).ConfigureAwait(false);
                        var networkStream = new NetworkStream(socket, ownsSocket: true);
                        return new MahoTlsFragmentedStream(networkStream, splitDelayMs);
                    }
                    catch (Exception ex)
                    {
                        socket.Dispose();
                        lastException = ex;
                    }
                }

                throw lastException ?? new SocketException((int) SocketError.HostNotFound);
            }
        };
    }

    public static bool IsPixivHost(string host) =>
        host.Equals("pixiv.net", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".pixiv.net", StringComparison.OrdinalIgnoreCase)
        || host.Equals("pximg.net", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".pximg.net", StringComparison.OrdinalIgnoreCase);
}
