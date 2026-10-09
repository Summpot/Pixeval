// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Misaki;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;

namespace Pixeval.Utilities.Network;

public sealed class PixivArtworkService : IGetArtworkService, IDownloadHttpClientService, IPostFavoriteService, IDisposable
{
    private readonly MakoClient _makoClient;
    private readonly MahoTransport _mahoTransport;
    private readonly NetworkSettingsGroup? _networkSettings;
    private readonly Lock _gate = new();
    private HttpClient? _apiClient;
    private HttpClient? _imageDownloadClient;
    private bool _disposed;

    public string Platform => IPlatformInfo.Pixiv;

    public PixivArtworkService(
        MakoClient makoClient,
        MahoTransport mahoTransport,
        NetworkSettingsGroup? networkSettings = null)
    {
        _makoClient = makoClient;
        _mahoTransport = mahoTransport;
        _networkSettings = networkSettings;
    }

    public async Task<IArtworkInfo> GetArtworkAsync(string id, CancellationToken token = default)
    {
        var rawId = long.Parse(id);
        return await _makoClient.GetIllustrationAsync(rawId);
    }

    public async Task<bool> PostFavoriteAsync(string id, bool favorite, CancellationToken token = default)
    {
        var rawId = long.Parse(id);
        var result = favorite
            ? await _makoClient.PostBookmarkAsync(false, rawId, "public", null)
            : await _makoClient.RemoveBookmarkAsync(false, rawId);
        return result.Success;
    }

    public HttpClient GetApiClient()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _apiClient ??= CreateApiClient();
        }
    }

    public HttpClient GetImageDownloadClient()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _imageDownloadClient ??= CreateImageDownloadClient();
        }
    }

    private HttpClient CreateApiClient()
    {
        var settings = _networkSettings ?? App.AppViewModel?.AppSettings?.NetworkSettings;
        return _mahoTransport.CreateHttpClient(networkSettings: settings);
    }

    private HttpClient CreateImageDownloadClient()
    {
        var settings = _networkSettings ?? App.AppViewModel?.AppSettings?.NetworkSettings;
        var client = _mahoTransport.CreateHttpClient(networkSettings: settings);
        client.DefaultRequestHeaders.Referrer = new Uri("https://app-api.pixiv.net/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PixivAndroidApp/6.199.0 (Android 15.0; Pixel 8)");
        return client;
    }

    public void Reset()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _apiClient?.Dispose();
            _apiClient = null;
            _imageDownloadClient?.Dispose();
            _imageDownloadClient = null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _apiClient?.Dispose();
            _apiClient = null;
            _imageDownloadClient?.Dispose();
            _imageDownloadClient = null;
        }
    }
}
