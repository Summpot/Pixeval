// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Options;
using Pixeval.Utilities.Network;

namespace Pixeval.Utilities.GitHub;

public sealed class GitHubHttpClientProvider(NetworkSettingsGroup networkSettings) : IDisposable
{
    public const string PlatformKey = "github";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, HttpClient> _clients = [];
    private bool _disposed;

    public string Platform => PlatformKey;

    public HttpClient GetApiClient() => GetClient(TimeSpan.FromSeconds(60));

    public HttpClient GetImageDownloadClient() => GetClient(TimeSpan.FromSeconds(60));

    public HttpClient GetUpdateDownloadClient() => GetClient(Timeout.InfiniteTimeSpan);

    private HttpClient GetClient(TimeSpan timeout)
    {
        var cacheKey = $"{GetCacheKey()};timeout:{timeout.Ticks}";
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_clients.TryGetValue(cacheKey, out var client))
                return client;

            client = GitHubDirectHttpClientFactory.Create(networkSettings, timeout);
            _clients.Add(cacheKey, client);
            return client;
        }
    }

    private string GetCacheKey()
    {
        var domainFronting = networkSettings.GitHubDomainFronting.EnableGitHubDomainFronting ? "domain-fronting" : "direct";
        var proxy = networkSettings.ProxySettings.ProxyType switch
        {
            ProxyType.None => "proxy:disabled",
            ProxyType.Custom => $"proxy:explicit:{ProxyHelper.NormalizeProxyUri(networkSettings.ProxySettings.Proxy) ?? ""}",
            _ => "proxy:auto"
        };
        return $"{domainFronting};{proxy}";
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var client in _clients.Values)
                client.Dispose();
            _clients.Clear();
        }
    }
}
