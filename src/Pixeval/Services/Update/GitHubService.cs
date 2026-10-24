using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Update;

namespace Pixeval.Services.Update;

public sealed class GitHubService(NetworkSettingsGroup settings) : IDisposable
{
    public const string PlatformKey = "github";
    private readonly Lock _gate = new();
    private ClientSlot? _current;
    private bool _disposed;
    private sealed class ClientSlot(GithubClient client)
    {
        public GithubClient Client { get; } = client;
        public int Requests;
        public bool Retired;
    }
    private ClientSlot Acquire()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var slot = _current ??= new(GithubClient.CreateFromSettings(settings));
            slot.Requests++;
            return slot;
        }
    }
    private void Release(ClientSlot slot)
    {
        lock (_gate)
            if (--slot.Requests == 0 && slot.Retired) slot.Client.Dispose();
    }
    private async Task<T> RunAsync<T>(Func<GithubClient, Task<T>> operation)
    {
        var slot = Acquire();
        try { return await operation(slot.Client).ConfigureAwait(false); }
        catch (UniFFISharp.Exceptions.InternalException ex) when (ex.Message.Contains("status: 3"))
        {
            throw new OperationCanceledException("Rust operation cancelled", ex);
        }
        finally { Release(slot); }
    }
    public void Reset()
    {
        lock (_gate)
        {
            if (_current is not { } slot) return;
            _current = null;
            slot.Retired = true;
            if (slot.Requests == 0) slot.Client.Dispose();
        }
    }
    public UpdateState CompareVersions(string current, string latest)
    {
        var slot = Acquire();
        try { return slot.Client.CompareVersions(current, latest); }
        finally { Release(slot); }
    }
    public Task<UpdateCheckResult> CheckForUpdatesAsync(string version, bool includePrereleases) =>
        RunAsync(client => client.CheckForUpdatesAsync(version, includePrereleases));
    public Task<List<AppRelease>> GetReleasesAsync(bool includePrereleases) =>
        RunAsync(client => client.GetReleasesAsync(includePrereleases));
    public Task<GitHubUserProfile> GetUserProfileAsync(string username, CancellationToken token = default) =>
        RunAsync(client => client.GetUserProfileAsync(username, token));
    public Task<byte[]> DownloadBytesAsync(string url, CancellationToken token = default) =>
        RunAsync(client => client.DownloadBytesAsync(url, null, 1, token));
    public Task<string> DownloadAssetWithProgressAsync(ReleaseAsset asset, string path, IProgress<int>? progress, CancellationToken token) =>
        RunAsync(client => client.DownloadAssetWithProgressAsync(asset, path, progress, token));
    public Task<string> DownloadUpdateTextAsync(string url, IDictionary<string, string>? headers, double? timeout) =>
        RunAsync(client => client.DownloadUpdateTextAsync(url, headers, timeout));
    public Task<byte[]> DownloadUpdateBytesAsync(string url, IDictionary<string, string>? headers, double? timeout) =>
        RunAsync(client => client.DownloadUpdateBytesAsync(url, headers, timeout));
    public Task<string> DownloadFileWithProgressAsync(string url, string path, string? expectedSha256, IProgress<int>? progress, CancellationToken cancellationToken, IDictionary<string, string>? headers, double? timeoutMinutes) =>
        RunAsync(client => client.DownloadFileWithProgressAsync(url, path, expectedSha256, progress, cancellationToken, headers, timeoutMinutes));
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            Reset();
        }
    }
}
