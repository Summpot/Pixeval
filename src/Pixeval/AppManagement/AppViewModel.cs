// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Extensions;
using Pixeval.Native.Config;
#if PIXEVAL_MCP
using Pixeval.Models.McpServer;
#endif
using Pixeval.Models.Navigation;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Booru;
using Pixeval.Native.Download;
using Pixeval.Native.Maho;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Utilities;
using Pixeval.Services;
using Pixeval.Utilities.Network;
using Pixeval.Utilities.GitHub;
using Pixeval.Views;
using Pixeval.Views.Home;

namespace Pixeval.AppManagement;

public sealed class AppViewModel(App app, FileLogger logger) : IAsyncDisposable
{
    private bool _isDisposed;

    public ServiceProvider AppServiceProvider { get; private set; } = null!;
    public IUserSessionService UserSession { get; private set; } = null!;
    public INavigationService NavigationService { get; private set; } = null!;
    public IAppUpdateNotificationCoordinator AppUpdateNotificationCoordinator { get; private set; } = null!;
    public App App { get; } = app;
    public StorageEngine StorageEngine { get; } = new(AppInfo.DatabaseFilePath);
    public DownloadManager DownloadManager { get; private set; } = null!;
    private readonly ObservableCollection<SearchHistoryRecord> _searchHistoryEntries = [];
    private readonly ObservableCollection<HomePageCardLayout> _homePageCards =
        AppInfo.LoadHomePageCards(logger) ?? HomePageCardsSettings.CreateDefaultCards();

    public SearchHistorySession SearchHistory { get; private set; } = null!;
    public HomePageCardSession HomePageCardsSession { get; private set; } = null!;
    public NavigationMenuDocument NavigationMenu { get; } = new(AppInfo.LoadNavigationMenuYaml(logger) ?? NavigationMenuYaml.DefaultYaml);
    public INetworkRuntime NetworkRuntime { get; private set; } = null!;
    public ObservableCollection<SearchHistoryRecord> SearchHistoryEntries => _searchHistoryEntries;
    public Task RestoreTask { get; private set; } = Task.CompletedTask;
    public MakoClient MakoClient { get; private set; } = null!;
    public MahoClient MahoClient { get; private set; } = null!;
    public AppSettings AppSettings { get; } = AppInfo.LoadAppSettings(logger) ?? new AppSettings();
    public LoginContext LoginContext { get; } = AppInfo.LoadLoginContext(logger) ?? new LoginContext();
    public ObservableCollection<HomePageCardLayout> HomePageCards => _homePageCards;
    public string NavigationMenuYamlText
    {
        get => NavigationMenu.Text;
        set => NavigationMenu.Text = value;
    }

    public void ResetHomePageCards() => HomePageCardsSession.Reset();

    public void InitializeProvider()
    {
        AppInfo.AppVersion.Attach(AppSettings, logger);
        AppSettings.Initialize();
        AppServiceProvider = CreateServiceProvider();
        UserSession = AppServiceProvider.GetRequiredService<IUserSessionService>();
        NavigationService = AppServiceProvider.GetRequiredService<INavigationService>();
        AppUpdateNotificationCoordinator = AppServiceProvider.GetRequiredService<IAppUpdateNotificationCoordinator>();
        UserSession.UserRefreshed += u => UserRefreshed?.Invoke(u);
        SetNameResolvers();
        InitializePersistence();
        if (GetCurrentLoginUser() is { } currentUser)
        {
            MakoClient.SetRefreshToken(currentUser.RefreshToken);
            MakoClient.SetUser(currentUser.TokenUser);
        }
        _ = AppServiceProvider.GetRequiredService<ExtensionService>();
        var images = AppServiceProvider.GetRequiredService<IImageProviderService>();
        _ = images.EnforceCacheSizeLimitAsync();
        images.UpdateNetworkOptions(AppSettings.ToMakoConfiguration());
    }

    public MahoClientOptions CreateMahoClientOptions()
    {
        var networkSettings = AppSettings.NetworkSettings;
        var df = networkSettings.PixivDomainFronting;
        var hostIps = new Dictionary<string, List<string>>
        {
            [MakoHttpOptions.AppApiHost] = [.. df.PixivAppApiNameResolver],
            [MakoHttpOptions.OAuthHost] = [.. df.PixivOAuthNameResolver],
            [MakoHttpOptions.WebApiHost] = [.. df.PixivWebApiNameResolver],
            [MakoHttpOptions.AccountHost] = [.. df.PixivAccountNameResolver],
            [MakoHttpOptions.ImageHost] = [.. df.PixivImageNameResolver],
            [MakoHttpOptions.ImageHost2] = [.. df.PixivImageNameResolver2]
        };
        return new MahoClientOptions(
            df.EnablePixivDomainFronting,
            SplitDelayMs: 100,
            hostIps,
            ProxyUrl: ProxyHelper.GetEffectiveProxyUrl(networkSettings));
    }

    private ServiceProvider CreateServiceProvider()
    {
        var makoConfig = AppSettings.ToMakoConfiguration();
        MakoClient = new MakoClient(makoConfig);
        MakoClient.SetSessionCallback(new MakoSessionCallbackHandler(this, logger));
        MahoClient = new MahoClient(CreateMahoClientOptions());
        DownloadManager = new DownloadManager(null, AppSettings.DownloadSettings.MaxDownloadTaskConcurrencyLevel);
        DownloadManager.BindStorage(StorageEngine);
        DownloadManager.BindMako(MakoClient);
        DownloadManager.SetDownloadPolicy(CreateDownloadPolicy(AppSettings));
        DownloadManager.AttachPageSnapshotCallback();
        var extensionService = new ExtensionService(logger, AppSettings);
        DownloadManager.SetFormatEncoder(new DownloadFormatEncoderAdapter(extensionService));
        SearchHistory = new SearchHistorySession(StorageEngine, _searchHistoryEntries);
        HomePageCardsSession = new HomePageCardSession(_homePageCards);
        var images = new ImageProviderService(AppSettings, logger);
        var networkRuntime = new NetworkRuntime(AppSettings, MakoClient, MahoClient, CreateMahoClientOptions, images);
        NetworkRuntime = networkRuntime;
        var gitHubHttp = new GitHubHttpClientProvider(AppSettings.NetworkSettings);

        try
        {
            var validIds = StorageEngine.GetAllSubscriptions().Select(s => s.HistoryEntryId).ToList();
            StorageEngine.DownloadRepository.DeleteOrphanSubscriptionDownloads(validIds);
        }
        catch (Exception ex)
        {
            logger.LogError(nameof(CreateServiceProvider), ex);
        }

        return new ServiceCollection()
            .AddSingleton(_ => logger)
            .AddSingleton(AppSettings)
            .AddSingleton(LoginContext)
            .AddBooruServices()
            .AddSingleton(gitHubHttp)
            .AddKeyedSingleton(GitHubHttpClientProvider.PlatformKey, gitHubHttp)
            .AddSingleton(SearchHistory)
            .AddSingleton(HomePageCardsSession)
            .AddSingleton(NavigationMenu)
            .AddSingleton<INetworkRuntime>(networkRuntime)
            .AddSingleton(new PixevalSettings(AppSettings))
            .AddSingleton(_ => MakoClient)
            .AddSingleton(_ => MahoClient)
            .AddSingleton(_ => StorageEngine)
            .AddSingleton(_ => DownloadManager)
            .AddSingleton<IUserSessionService, UserSessionService>()
            .AddSingleton<IImageProviderService>(images)
            .AddSingleton<IDownloadFormatService, DownloadFormatService>()
            .AddSingleton<IArtworkActionService, ArtworkActionService>()
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<IAppUpdateNotificationCoordinator, AppUpdateNotificationCoordinator>()
            .AddSingleton<HomeCardDefinitions>()
            .AddSingleton<WorkSubscriptionDownloadService>()
            .AddSingleton<IWorkSubscriptionService>(provider =>
                provider.GetRequiredService<WorkSubscriptionDownloadService>())
            .AddSingleton(extensionService)
            .AddTransient<ViewModels.SettingsPageViewModel>()
#if PIXEVAL_MCP
            .AddSingleton<IPixevalMcpService>(t =>
                new PixevalMcpService(this, t.GetRequiredService<FileLogger>()))
#endif
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private void InitializePersistence()
    {
        ArtworkUiStateStore.Initialize(StorageEngine);
        StorageEngine.InitializeObserver();
        RestoreTask = RestoreHistoryAsync();
    }

    private async Task RestoreHistoryAsync()
    {
        try
        {
            var searches = await Task.Run(() => StorageEngine.StreamSearchHistories(0, 50));
            foreach (var s in searches)
                SearchHistoryEntries.Add(s);

            await Task.Run(() =>
            {
                DownloadManager.SetSubscriptions(WorkSubscriptionDownloadService.CreateFolderMetas(StorageEngine));
                DownloadManager.RestoreHistories();
            });
        }
        catch (Exception ex)
        {
            logger.LogError(nameof(RestoreHistoryAsync), ex);
        }
    }

    public void AddSearchHistory(string text, string? translatedName = null) =>
        SearchHistory.Add(text, translatedName);

    public void ClearSearchHistory() => SearchHistory.Clear();

    public void AddBrowseHistory(object entry) => StorageEngine.HistoryRepository.AddBrowseHistory(entry);

    public void ClearBrowseHistory() => StorageEngine.HistoryRepository.Clear();

    public bool ContainsWatchLater(object entry) => StorageEngine.WatchLaterRepository.ContainsWatchLater(entry);

    public bool AddWatchLater(object entry) => StorageEngine.WatchLaterRepository.AddWatchLater(entry);

    public bool RemoveWatchLater(object entry) => StorageEngine.WatchLaterRepository.RemoveWatchLater(entry);

    public static DownloadPolicy CreateDownloadPolicy(AppSettings settings)
    {
        var download = settings.DownloadSettings;
        return new DownloadPolicy(
            download.OverwriteDownloadedFile,
            download.DownloadFormats.IllustrationDownloadFormat,
            download.DownloadFormats.UgoiraDownloadFormat,
            download.DownloadFormats.NovelDownloadFormat);
    }

    public void ClearDownloadHistories()
    {
        DownloadManager.ClearTasks();
        StorageEngine.ClearDownloadHistory();
        StorageEngine.ClearSubscriptionDownloadHistory();
    }

    public Task RemoveWorkSubscriptionDownloadsAsync(long workSubscriptionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workSubscriptionId);
        DownloadManager.RemoveSubscription(workSubscriptionId);
        return Task.CompletedTask;
    }

    public void OnTokenRefreshed(TokenResponse? tokenResponse) =>
        UserSession.OnTokenRefreshed(tokenResponse);

    public event Action<TokenUser?>? UserRefreshed;

    public LoginUserRecord? GetCurrentLoginUser() =>
        UserSession.GetCurrentLoginUser();

    public void QueueWorkSubscriptionSyncAll() =>
        AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>().QueueSyncAll();

    public void QueueWorkSubscriptionSync(WorkSubscriptionRecord subscription) =>
        AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>().QueueSyncSubscription(subscription);

    public void QueueWorkSubscriptionInitialSync(WorkSubscriptionRecord subscription, IFetchEngine<IWorkEntry>? sourceEngine = null) =>
        AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>().QueueInitialSync(subscription, sourceEngine);

    public void QueueWorkSubscriptionSyncCurrentSource(long uid, WorkSubscriptionType subscriptionType, WorkSubscriptionWorkKind workKind, IFetchEngine<IWorkEntry> engine) =>
        AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>().QueueSyncCurrentSource(uid, subscriptionType, workKind, engine);

    public void SetNameResolvers() => NetworkRuntime.AttachNameResolverHooks();

    public void UpdateMakoNetworkOptions() => NetworkRuntime.UpdateNetworkOptions();

    public HttpClient GetRequiredGitHubHttpClient() =>
        AppServiceProvider.GetRequiredKeyedService<GitHubHttpClientProvider>(GitHubHttpClientProvider.PlatformKey).GetApiClient();

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        try
        {
            DownloadManager?.MarkDisposed();
            DownloadManager?.Dispose();
            MahoClient?.Dispose();
            await AppServiceProvider.DisposeAsync();
        }
        catch
        {
            // ignored
        }
    }

    private sealed class MakoSessionCallbackHandler(AppViewModel appViewModel, FileLogger logger) : IMakoSessionCallback
    {
        public void OnAuthInvalidated(string message)
        {
            logger.LogWarning($"Auth invalidated: {message}", null);
            Dispatcher.UIThread.Post(() => appViewModel.OnTokenRefreshed(null));
        }

        public void OnRateLimitEncountered(ulong retryAfterSecs)
        {
            logger.LogWarning($"Rate limited by Pixiv API. Retry-After: {retryAfterSecs}s", null);
        }
    }
}
