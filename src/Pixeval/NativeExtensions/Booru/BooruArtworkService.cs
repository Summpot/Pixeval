// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Misaki;

namespace Pixeval.Native.Booru;

public class GeneralImageDownloader : IDownloadHttpClientService
{
    public string Platform => IPlatformInfo.All;

    private static readonly Lazy<HttpClient> s_httpClient = new(() =>
    {
        var client = new HttpClient();
        IReadOnlyList<ProductInfoHeaderValue> ua =
        [
            new("Mozilla", "5.0"),
            new("(Windows NT 10.0; Win64; x64)"),
            new("AppleWebKit", "537.36"),
            new("(KHTML, like Gecko)"),
            new("Chrome", "133.0.0.0"),
            new("Safari", "537.36"),
            new("Edg", "133.0.0.0")
        ];
        foreach (var item in ua)
            client.DefaultRequestHeaders.UserAgent.Add(item);
        return client;
    });

    public HttpClient GetApiClient() => s_httpClient.Value;

    public HttpClient GetImageDownloadClient() => s_httpClient.Value;
}

public class DanbooruImageDownloader : IDownloadHttpClientService
{
    public string Platform => IPlatformInfo.Danbooru;

    private static readonly Lazy<HttpClient> s_httpClient = new(() =>
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new("gdl", "1.24.5"));
        return client;
    });

    public HttpClient GetApiClient() => s_httpClient.Value;

    public HttpClient GetImageDownloadClient() => s_httpClient.Value;
}

public sealed class BooruArtworkService : IGetArtworkService, IPostFavoriteService, IDownloadHttpClientService
{
    private readonly BooruClient _client;
    private readonly BooruPlatform _platform;
    private readonly IDownloadHttpClientService _downloader;

    public string Platform => _platform.ToPlatformString();

    public BooruArtworkService(BooruClient client, BooruPlatform platform, IDownloadHttpClientService? downloader = null)
    {
        _client = client;
        _platform = platform;
        _downloader = downloader ?? (platform == BooruPlatform.Danbooru ? new DanbooruImageDownloader() : new GeneralImageDownloader());
    }

    public async Task<IArtworkInfo> GetArtworkAsync(string id, CancellationToken token = default)
    {
        return await _client.GetPostAsync(_platform, id);
    }

    public async Task<bool> PostFavoriteAsync(string id, bool favorite, CancellationToken token = default)
    {
        var result = await _client.PostFavoriteAsync(_platform, id, favorite);
        return result.Success;
    }

    public HttpClient GetApiClient() => _downloader.GetApiClient();

    public HttpClient GetImageDownloadClient() => _downloader.GetImageDownloadClient();
}

public static class BooruServiceExtensions
{
    public static IServiceCollection AddBooruServices(this IServiceCollection services)
    {
        var booruClient = new BooruClient(null);
        services.AddSingleton(booruClient);

        var generalDownloader = new GeneralImageDownloader();
        var danbooruDownloader = new DanbooruImageDownloader();

        services.AddKeyedSingleton<IDownloadHttpClientService>(IPlatformInfo.All, (_, _) => generalDownloader);
        services.AddKeyedSingleton<IDownloadHttpClientService>(IPlatformInfo.Danbooru, (_, _) => danbooruDownloader);

        BooruPlatform[] platforms = [
            BooruPlatform.Danbooru,
            BooruPlatform.Gelbooru,
            BooruPlatform.Yandere,
            BooruPlatform.Sankaku,
            BooruPlatform.Rule34
        ];

        foreach (var platform in platforms)
        {
            var platformKey = platform.ToPlatformString();
            var downloader = platform == BooruPlatform.Danbooru ? (IDownloadHttpClientService) danbooruDownloader : generalDownloader;
            var service = new BooruArtworkService(booruClient, platform, downloader);

            services.AddKeyedSingleton<IGetArtworkService>(platformKey, (_, _) => service);
            services.AddKeyedSingleton<IPostFavoriteService>(platformKey, (_, _) => service);
            services.AddKeyedSingleton<IDownloadHttpClientService>(platformKey, (_, _) => service);
        }

        return services;
    }
}
