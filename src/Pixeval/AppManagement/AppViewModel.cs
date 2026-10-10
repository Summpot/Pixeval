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
using Pixeval.Models.Download;
using Pixeval.Models.Download.Tasks;
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
using Pixeval.Utilities.IO.Caching;
using Pixeval.Views;

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
    public ObservableCollection<SearchHistoryRecord> SearchHistoryEntries { get; } = [];
    public Task RestoreTask { get; private set; } = Task.CompletedTask;
    public MakoClient MakoClient { get; private set; } = null!;
    public MahoClient MahoClient { get; private set; } = null!;
    public AppSettings AppSettings { get; } = AppInfo.LoadAppSettings(logger) ?? new AppSettings();
    public LoginContext LoginContext { get; } = AppInfo.LoadLoginContext(logger) ?? new LoginContext();
    public ObservableCollection<HomePageCardLayout> HomePageCards { get; } = AppInfo.LoadHomePageCards(logger) ?? HomePageCardsSettings.CreateDefaultCards();
    public string NavigationMenuYamlText { get; set; } = AppInfo.LoadNavigationMenuYaml(logger) ?? NavigationMenuYaml.DefaultYaml;

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
        UserSession = AppServiceProvider.GetRequiredService<IUserSessionService>();
        NavigationService = AppServiceProvider.GetRequiredService<INavigationService>();
        AppUpdateNotificationCoordinator = AppServiceProvider.GetRequiredService<IAppUpdateNotificationCoordinator>();
        UserSession.UserRefreshed += u => UserRefreshed?.Invoke(u);
        UserSession.UserRefreshed += _ => PixevalSettings.Instance.OnIsLoggedInChanged();
        SetNameResolvers();
        InitializePersistence();
        if (GetCurrentLoginUser() is { } currentUser)
        {
            MakoClient.SetRefreshToken(currentUser.RefreshToken);
            MakoClient.SetUser(currentUser.TokenUser);
        }
        PixevalSettings.Instance.OnIsLoggedInChanged();
        _ = AppServiceProvider.GetRequiredService<ExtensionService>();
        _ = CacheHelper.EnforceCacheSizeLimitAsync();
        CacheHelper.UpdateNetworkOptions(AppSettings.ToMakoConfiguration());
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
            .AddKeyedSingleton<GitHubHttpClientProvider>(
                GitHubHttpClientProvider.PlatformKey,
                (_, _) => new GitHubHttpClientProvider(AppSettings.NetworkSettings))
            .AddSingleton(_ => MakoClient)
            .AddSingleton(_ => MahoClient)
            .AddSingleton(_ => StorageEngine)
            .AddSingleton(_ => DownloadManager)
            .AddSingleton<IUserSessionService, UserSessionService>()
            .AddSingleton<IImageProviderService, ImageProviderService>()
            .AddSingleton<IDownloadFormatService, DownloadFormatService>()
            .AddSingleton<IArtworkActionService, ArtworkActionService>()
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<IAppUpdateNotificationCoordinator, AppUpdateNotificationCoordinator>()
            .AddSingleton<WorkSubscriptionDownloadService>()
            .AddSingleton<IWorkSubscriptionService>(provider =>
                provider.GetRequiredService<WorkSubscriptionDownloadService>())
            .AddSingleton<IllustrationDownloadTaskFactory>()
            .AddSingleton<NovelDownloadTaskFactory>()
            .AddSingleton(provider => new ExtensionService(provider.GetRequiredService<FileLogger>(), AppSettings))
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

            var regular = await Task.Run(() => StorageEngine.DownloadRepository.StreamDownloadHistoryCursor(null, 50));
            foreach (var r in regular)
            {
                if (r.Entry is not null && r.ToTaskGroup() is { } task)
                    _ = DownloadManager.TryRestoreTask(task);
            }

            var sub = await Task.Run(() => StorageEngine.DownloadRepository.StreamSubscriptionDownloadHistoryCursor(null, 50));
            foreach (var s in sub)
            {
                if (s.Entry is not null && s.ToTaskGroup() is { } task)
                    _ = DownloadManager.TryRestoreTask(task);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(nameof(RestoreHistoryAsync), ex);
        }
    }

    public void AddSearchHistory(string text, string? translatedName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var entry = StorageEngine.UpsertSearchHistory(text, translatedName, DateTimeOffset.UtcNow.ToString("O"));
        if (SearchHistoryEntries.FirstOrDefault(e => e.Value == text) is { } existing)
            SearchHistoryEntries.Remove(existing);
        SearchHistoryEntries.Insert(0, entry);
    }

    public void ClearSearchHistory()
    {
        StorageEngine.ClearSearchHistory();
        SearchHistoryEntries.Clear();
    }

    public void AddBrowseHistory(object entry) => StorageEngine.HistoryRepository.AddBrowseHistory(entry);

    public void ClearBrowseHistory() => StorageEngine.HistoryRepository.Clear();

    public bool ContainsWatchLater(object entry) => StorageEngine.WatchLaterRepository.ContainsWatchLater(entry);

    public bool AddWatchLater(object entry) => StorageEngine.WatchLaterRepository.AddWatchLater(entry);

    public bool RemoveWatchLater(object entry) => StorageEngine.WatchLaterRepository.RemoveWatchLater(entry);

    public void UpdateDownloadHistory(IDownloadHistoryEntry entry) => StorageEngine.DownloadRepository.Update(entry);

    public async Task QueueSubscriptionDownloadBatchAsync(IReadOnlyList<IDownloadTaskGroup> taskGroups)
    {
        ArgumentNullException.ThrowIfNull(taskGroups);
        if (taskGroups.Count is 0)
            return;

        var entries = taskGroups
            .Select(static task => task.DatabaseEntry)
            .OfType<SubscriptionDownloadHistoryRecord>()
            .ToList();

        await Task.Run(() => StorageEngine.DownloadRepository.AddOrReplaceSubscriptionDownloadHistoryBatch(entries)).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var taskGroup in taskGroups)
                DownloadManager.QueueTask(taskGroup);
        });
    }

    public async Task RemoveWorkSubscriptionDownloadsAsync(int workSubscriptionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workSubscriptionId);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var task in DownloadManager.QueuedTasks
                         .Where(task => task is IDownloadTaskGroup { DatabaseEntry: SubscriptionDownloadHistoryRecord { WorkSubscriptionId: var id } } && id == workSubscriptionId)
                         .ToArray())
                _ = DownloadManager.TryRemoveTask(task);
        });
        await Task.Run(() => StorageEngine.DownloadRepository.DeleteSubscriptionDownloadsByWorkSubscriptionId(workSubscriptionId)).ConfigureAwait(false);
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

    public void SetNameResolvers()
    {
        var networkSettings = AppSettings.NetworkSettings;
        HookResolver(networkSettings.PixivDomainFronting.PixivAppApiNameResolver);
        HookResolver(networkSettings.PixivDomainFronting.PixivImageNameResolver);
        HookResolver(networkSettings.PixivDomainFronting.PixivImageNameResolver2);
        HookResolver(networkSettings.PixivDomainFronting.PixivOAuthNameResolver);
        HookResolver(networkSettings.PixivDomainFronting.PixivAccountNameResolver);
        HookResolver(networkSettings.PixivDomainFronting.PixivWebApiNameResolver);

        void HookResolver(ObservableCollection<string> ips)
        {
            ips.CollectionChanged += (_, _) =>
            {
                UpdateMakoNetworkOptions();
            };
        }
    }

    public void UpdateMakoNetworkOptions()
    {
        var config = AppSettings.ToMakoConfiguration();
        MakoClient.UpdateConfiguration(config);
        CacheHelper.UpdateNetworkOptions(config);
        MahoClient?.UpdateOptions(CreateMahoClientOptions());
        AppInfo.AppVersion.ResetUpdateEngine();
    }

    public HttpClient GetRequiredGitHubHttpClient() =>
        AppServiceProvider.GetRequiredKeyedService<GitHubHttpClientProvider>(GitHubHttpClientProvider.PlatformKey).GetApiClient();

    public HttpClient GetRequiredGitHubUpdateHttpClient() =>
        AppServiceProvider.GetRequiredKeyedService<GitHubHttpClientProvider>(GitHubHttpClientProvider.PlatformKey).GetUpdateDownloadClient();

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
