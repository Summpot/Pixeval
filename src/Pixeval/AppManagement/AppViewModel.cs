// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Imouto.BooruParser;
using Microsoft.Extensions.DependencyInjection;
using Misaki;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Database;
using Pixeval.Models.Database.Managers;
using Pixeval.Models.Download;
using Pixeval.Models.Extensions;
using Pixeval.Models.Home;
#if PIXEVAL_MCP
using Pixeval.Models.McpServer;
#endif
using Pixeval.Models.Navigation;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Storage;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.Utilities.GitHub;
using Pixeval.Utilities.IO.Caching;
using Pixeval.Utilities.Network;
using Pixeval.Views;

namespace Pixeval.AppManagement;

public sealed class AppViewModel(App app, FileLogger logger) : IAsyncDisposable
{
    private bool _disposed;

    public ServiceProvider AppServiceProvider { get; private set; } = null!;

    public App App { get; } = app;

    public HistoryPersistHelper HistoryPersistHelper => AppServiceProvider.GetRequiredService<HistoryPersistHelper>();

    public MakoClient MakoClient { get; private set; } = null!;

    public MahoTransport MahoTransport { get; } = new();

    public AppSettings AppSettings { get; } = AppInfo.LoadAppSettings(logger) ?? new AppSettings();

    public LoginContext LoginContext { get; } = AppInfo.LoadLoginContext(logger) ?? new LoginContext();

    public ObservableCollection<HomePageCardLayout> HomePageCards { get; } = AppInfo.LoadHomePageCards(logger) ?? HomePageCardsSettings.CreateDefaultCards();

    public string NavigationMenuYamlText { get; set; } =
        AppInfo.LoadNavigationMenuYaml(logger) ?? NavigationMenuYaml.DefaultYaml;

    public void ResetHomePageCards()
    {
        HomePageCards.Clear();
        foreach (var card in HomePageCardsSettings.CreateDefaultCards())
            HomePageCards.Add(card);
    }

    public void InitializeProvider()
    {
        AppSettings.Initialize();
        AppServiceProvider = CreateServiceProvider();
        SetNameResolvers();
        if (GetCurrentLoginUser() is { } currentUser)
        {
            MakoClient.SetRefreshToken(currentUser.RefreshToken);
            MakoClient.SetUser(currentUser.TokenUser);
        }
        // 触发卸载插件
        _ = AppServiceProvider.GetRequiredService<ExtensionService>();
        _ = CacheHelper.EnforceCacheSizeLimitAsync();
    }

    private ServiceProvider CreateServiceProvider()
    {
        var makoConfig = AppSettings.ToMakoConfiguration();
        MakoClient = new MakoClient(makoConfig);
        var pixivService = new PixivArtworkService(MakoClient, MahoTransport, AppSettings.NetworkSettings);

        return new ServiceCollection()
            .AddSingleton(_ => logger)
            .AddBooruParsers()
            .AddKeyedSingleton<IGetArtworkService>(IPlatformInfo.Pixiv, (provider, key) => pixivService)
            .AddKeyedSingleton<IDownloadHttpClientService>(IPlatformInfo.Pixiv, (provider, key) => pixivService)
            .AddKeyedSingleton<IPostFavoriteService>(IPlatformInfo.Pixiv, (provider, key) => pixivService)
            .AddKeyedSingleton<GitHubHttpClientProvider>(
                GitHubHttpClientProvider.PlatformKey,
                (_, _) => new GitHubHttpClientProvider(AppSettings.NetworkSettings))
            .AddKeyedSingleton<IDownloadHttpClientService>(
                GitHubHttpClientProvider.PlatformKey,
                (provider, key) => provider.GetRequiredKeyedService<GitHubHttpClientProvider>(key))
            .AddSingleton(_ => MakoClient)
            .AddSingleton<WorkSubscriptionDownloadService>()
            .AddSingleton<IWorkSubscriptionService>(provider =>
                provider.GetRequiredService<WorkSubscriptionDownloadService>())
            .AddSingleton<IllustrationDownloadTaskFactory>()
            .AddSingleton<NovelDownloadTaskFactory>()
            .AddSingleton(provider => new ExtensionService(provider.GetRequiredService<FileLogger>(), AppSettings))
            .AddSingleton(_ => new StorageEngine(AppInfo.DatabaseFilePath))
            .AddSingleton(provider => new DownloadHistoryPersistentManager(provider.GetRequiredService<StorageEngine>(), provider.GetRequiredService<FileLogger>()))
            .AddSingleton(provider => new SubscriptionDownloadHistoryPersistentManager(provider.GetRequiredService<StorageEngine>(), provider.GetRequiredService<FileLogger>()))
            .AddSingleton(provider => new WorkSubscriptionPersistentManager(provider.GetRequiredService<StorageEngine>()))
            .AddSingleton(provider => new BlockedUserPersistentManager(provider.GetRequiredService<StorageEngine>()))
            .AddSingleton(provider => new SearchHistoryPersistentManager(provider.GetRequiredService<StorageEngine>()))
            .AddSingleton(provider => new BrowseHistoryPersistentManager(provider.GetRequiredService<StorageEngine>(), provider.GetRequiredService<FileLogger>()))
            .AddSingleton(provider => new WatchLaterPersistentManager(provider.GetRequiredService<StorageEngine>(), provider.GetRequiredService<FileLogger>()))
            .AddSingleton(provider => new LoginUserPersistentManager(provider.GetRequiredService<StorageEngine>()))
            .AddSingleton<HistoryPersistHelper>()
#if PIXEVAL_MCP
            .AddSingleton<IPixevalMcpService>(t =>
                new PixevalMcpService(this, t.GetRequiredService<FileLogger>()))
#endif
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public void OnTokenRefreshed(TokenResponse? tokenResponse)
    {
        TokenUser? user = null;
        if (tokenResponse is null)
        {
            LoginContext.CurrentKey = 0;
            MakoClient.ClearToken();
        }
        else
        {
            user = tokenResponse.User ?? MakoClient.GetUser();
            if (user is not null)
            {
                var manager = AppServiceProvider.GetRequiredService<LoginUserPersistentManager>();
                var entry = manager.Upsert(LoginUserRecord.FromTokenUser(tokenResponse.RefreshToken, user));
                LoginContext.CurrentKey = (int)entry.HistoryEntryId;
            }
        }

        void Notify()
        {
            PixevalSettings.Instance.OnIsLoggedInChanged();
            UserRefreshed?.Invoke(user);
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            Notify();
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(Notify);

        AppInfo.SaveLoginContext(LoginContext);
    }

    public event Action<TokenUser?>? UserRefreshed;

    public LoginUserRecord? GetCurrentLoginUser()
    {
        return AppServiceProvider.GetRequiredService<LoginUserPersistentManager>()
            .GetByKey(LoginContext.CurrentKey);
    }

    public void QueueWorkSubscriptionSyncAll()
    {
        AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>().QueueSyncAll();
    }

    public void QueueWorkSubscriptionSync(WorkSubscriptionRecord subscription)
    {
        AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>()
            .QueueSyncSubscription(subscription);
    }

    public void QueueWorkSubscriptionInitialSync(
        WorkSubscriptionRecord subscription,
        IFetchEngine<IWorkEntry>? sourceEngine = null)
    {
        AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>()
            .QueueInitialSync(subscription, sourceEngine);
    }

    public void QueueWorkSubscriptionSyncCurrentSource(long uid, WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind, IFetchEngine<IWorkEntry> engine)
    {
        AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>()
            .QueueSyncCurrentSource(uid, subscriptionType, workKind, engine);
    }

    public void SetNameResolvers()
    {
        var networkSettings = AppSettings.NetworkSettings;
        SetMahoResolver(MakoHelper.AppApiHost, networkSettings.PixivDomainFronting.PixivAppApiNameResolver);
        SetMahoResolver(MakoHelper.ImageHost, networkSettings.PixivDomainFronting.PixivImageNameResolver);
        SetMahoResolver(MakoHelper.ImageHost2, networkSettings.PixivDomainFronting.PixivImageNameResolver2);
        SetMahoResolver(MakoHelper.OAuthHost, networkSettings.PixivDomainFronting.PixivOAuthNameResolver);
        SetMahoResolver(MakoHelper.AccountHost, networkSettings.PixivDomainFronting.PixivAccountNameResolver);
        SetMahoResolver(MakoHelper.WebApiHost, networkSettings.PixivDomainFronting.PixivWebApiNameResolver);
        return;

        void SetMahoResolver(string host, ObservableCollection<string> ips)
        {
            MahoTransport.SetHostIps(host, ips);
            ips.CollectionChanged += (sender, e) =>
            {
                if (sender is ObservableCollection<string> updatedIps)
                    MahoTransport.SetHostIps(host, updatedIps);
            };
        }
    }

    public void UpdateMakoNetworkOptions()
    {
        MakoClient.UpdateConfiguration(AppSettings.ToMakoConfiguration());
        (AppServiceProvider.GetKeyedService<IDownloadHttpClientService>(IPlatformInfo.Pixiv) as PixivArtworkService)?.Reset();
    }

    public T? GetPlatformService<T>(string platformKey) where T : IMisakiService
    {
        return AppServiceProvider.GetKeyedService<T>(platformKey)
               ?? AppServiceProvider.GetKeyedService<T>(IPlatformInfo.All);
    }

    public T GetRequiredPlatformService<T>(string platformKey) where T : IMisakiService
    {
        return AppServiceProvider.GetKeyedService<T>(platformKey)
               ?? AppServiceProvider.GetKeyedService<T>(IPlatformInfo.All)
               ?? throw new NotSupportedException($"No service found for {platformKey}");
    }

    public HttpClient GetRequiredHttpClient()
    {
        // 在 AddBooruParsers 中注册的
        return AppServiceProvider.GetRequiredKeyedService<IDownloadHttpClientService>(IPlatformInfo.All)
            .GetImageDownloadClient();
    }

    public HttpClient GetRequiredGitHubHttpClient() =>
        AppServiceProvider.GetRequiredKeyedService<IDownloadHttpClientService>(GitHubHttpClientProvider.PlatformKey)
            .GetApiClient();

    public HttpClient GetRequiredGitHubUpdateHttpClient() =>
        AppServiceProvider.GetRequiredKeyedService<GitHubHttpClientProvider>(GitHubHttpClientProvider.PlatformKey)
            .GetUpdateDownloadClient();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            if (AppServiceProvider.GetService<WorkSubscriptionDownloadService>() is { } subscriptionService)
                await subscriptionService.CancelAndWaitAsync();
            // 有些服务只能 DisposeAsync
            await AppServiceProvider.DisposeAsync();
            MahoTransport.Dispose();
            MakoClient.Dispose();
        }
        catch
        {
            // ignored
            // 保证退出时不出幺蛾子
        }
    }
}
