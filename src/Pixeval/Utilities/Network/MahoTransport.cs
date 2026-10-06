// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace Pixeval.Utilities.Network;

/// <summary>
/// 基于 Rust pixeval_maho 实现的网络抗审查传输辅助类。
/// </summary>
public sealed class MahoTransport : IDisposable
{
    private readonly DnsResolver _dnsResolver = new();
    private bool _disposed;

    public DnsResolver DnsResolver
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _dnsResolver;
        }
    }

    public void SetHostIps(string host, IEnumerable<string> ips)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _dnsResolver.SetMapping(host, [.. ips]);
    }

    public IReadOnlyList<string> GetHostIps(string host)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _dnsResolver.GetMapping(host);
    }

    public void ClearHostIps()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _dnsResolver.Clear();
    }

    public SocketsHttpHandler CreateHandler(int splitDelayMs = 100, AppManagement.Settings.NetworkSettingsGroup? networkSettings = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return MahoSocketsHttpHandlerFactory.CreateHandler(_dnsResolver, splitDelayMs, networkSettings);
    }

    public HttpClient CreateHttpClient(int splitDelayMs = 100, AppManagement.Settings.NetworkSettingsGroup? networkSettings = null)
    {
        return new HttpClient(CreateHandler(splitDelayMs, networkSettings), disposeHandler: true);
    }

    /// <summary>
    /// 对 TLS ClientHello 报文执行 SNI 探测与分片。
    /// </summary>
    public static IReadOnlyList<byte[]> SplitClientHello(byte[] packet)
    {
        var frags = PixevalMahoMethods.SplitClientHello([.. packet]);
        return [.. frags];
    }

    /// <summary>
    /// 定位 TLS ClientHello 报文中的 SNI 扩展域名。
    /// </summary>
    public static string? LocateServerName(byte[] packet) =>
        PixevalMahoMethods.LocateServerName([.. packet]);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dnsResolver.Dispose();
        GC.SuppressFinalize(this);
    }
}
