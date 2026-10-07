// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Imouto.BooruParser;
using Microsoft.Extensions.DependencyInjection;
using Misaki;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Extensions;
using Pixeval.Models.Home;
#if PIXEVAL_MCP
using Pixeval.Models.McpServer;
#endif
using Pixeval.Models.Navigation;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Download;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Utilities;
using Pixeval.Utilities.GitHub;
using Pixeval.Utilities.IO.Caching;
using Pixeval.Utilities.Network;
using Pixeval.Views;

namespace Pixeval.AppManagement;

public sealed class AppViewModel(App app, FileLogger logger) : IAsyncDisposable
{
    private readonly CancellationTokenSource _downloadRestoreCancellationTokenSource = new();
    private readonly HashSet<DownloadTaskKey> _removedDownloadHistoryKeys = [];
    private readonly HashSet<int> _removedWorkSubscriptionIds = [];
    private readonly HashSet<string> _removedSearchHistoryValues = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _searchRestoreCancellationTokenSource = new();
    private bool _isDownloadHistoryRestoreCompleted;
    private bool _isDisposed;
    private bool _isCommittingDownloadBatch;
    private bool _isRemovingSubscriptionDownloads;
    private bool _isRestoringDownloadHistory;
    private bool _isRestoringSearchHistory;
    private bool _isSearchHistoryRestoreCompleted;
    private bool _isUpdatingSearchHistory;

    public ServiceProvider AppServiceProvider { get; private set; } = null!;

    public App App { get; } = app;

    public StorageEngine StorageEngine { get; } = new(AppInfo.DatabaseFilePath);

    public DownloadManager DownloadManager { get; private set; } = null!;

    public ObservableCollection<SearchHistoryRecord> SearchHistoryEntries { get; } = [];

    public Task RestoreTask { get; private set; } = Task.CompletedTask;

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
        InitializePersistence();
        if (GetCurrentLoginUser() is { } currentUser)
        {
            MakoClient.SetRefreshToken(currentUser.RefreshToken);
            MakoClient.SetUser(currentUser.TokenUser);
        }
        // 触发卸载插件
        _ = AppServiceProvider.GetRequiredService<ExtensionService>();
        _ = CacheHelper.EnforceCacheSizeLimitAsync();
        CacheHelper.UpdateNetworkOptions(AppSettings.ToMakoConfiguration());
    }

    private ServiceProvider CreateServiceProvider()
    {
        var makoConfig = AppSettings.ToMakoConfiguration();
        MakoClient = new MakoClient(makoConfig);
        var pixivService = new PixivArtworkService(MakoClient, MahoTransport, AppSettings.NetworkSettings);
        DownloadManager = new DownloadManager(pixivService.GetImageDownloadClient(), AppSettings.DownloadSettings.MaxDownloadTaskConcurrencyLevel);

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
            .AddSingleton(_ => StorageEngine)
            .AddSingleton(_ => DownloadManager)
            .AddSingleton<WorkSubscriptionDownloadService>()
            .AddSingleton<IWorkSubscriptionService>(provider =>
                provider.GetRequiredService<WorkSubscriptionDownloadService>())
            .AddSingleton<IllustrationDownloadTaskFactory>()
            .AddSingleton<NovelDownloadTaskFactory>()
            .AddSingleton(provider => new ExtensionService(provider.GetRequiredService<FileLogger>(), AppSettings))
#if PIXEVAL_MCP
            .AddSingleton<IPixevalMcpService>(t =>
                new PixevalMcpService(this, t.GetRequiredService<FileLogger>()))
#endif
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private void InitializePersistence()
    {
        StorageEngine.InitializeObserver();
        SearchHistoryEntries.CollectionChanged += OnSearchHistoryCollectionChanged;
        DownloadManager.QueuedTasks.CollectionChanged += OnDownloadHistoryCollectionChanged;
        RestoreTask = Task.WhenAll(
            RestoreSafelyAsync(
                RestoreSearchHistoryAsync,
                nameof(RestoreSearchHistoryAsync),
                _searchRestoreCancellationTokenSource.Token),
            RestoreSafelyAsync(
                RestoreDownloadHistoryAsync,
                nameof(RestoreDownloadHistoryAsync),
                _downloadRestoreCancellationTokenSource.Token));
    }

    public void AddSearchHistory(string text, string? translatedName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var searchHistoryEntry = new SearchHistoryRecord(0, text, translatedName, DateTimeOffset.UtcNow.ToString("O"));
        _ = _removedSearchHistoryValues.Remove(text);
        StorageEngine.UpsertSearchHistory(text, translatedName, DateTimeOffset.UtcNow.ToString("O"));
        _isUpdatingSearchHistory = true;
        try
        {
            if (SearchHistoryEntries.FirstOrDefault(entry => entry.Value == text) is { } existing)
                SearchHistoryEntries.Remove(existing);
            SearchHistoryEntries.Insert(0, searchHistoryEntry);
        }
        finally
        {
            _isUpdatingSearchHistory = false;
        }
    }

    public void ClearSearchHistory()
    {
        _searchRestoreCancellationTokenSource.Cancel();
        StorageEngine.ClearSearchHistory();
        SearchHistoryEntries.Clear();
    }

    public void AddBrowseHistory(IArtworkInfo entry) => StorageEngine.HistoryRepository.AddBrowseHistory(entry);

    public void ClearBrowseHistory() => StorageEngine.HistoryRepository.Clear();

    public bool ContainsWatchLater(IArtworkInfo entry) => StorageEngine.WatchLaterRepository.ContainsWatchLater(entry);

    public bool AddWatchLater(IArtworkInfo entry) => StorageEngine.WatchLaterRepository.AddWatchLater(entry);

    public bool RemoveWatchLater(IArtworkInfo entry) => StorageEngine.WatchLaterRepository.RemoveWatchLater(entry);

    public void UpdateDownloadHistory(IDownloadHistoryEntry entry) => StorageEngine.DownloadRepository.Update(entry);

    public async Task QueueSubscriptionDownloadBatchAsync(IReadOnlyList<IDownloadTaskGroup> taskGroups)
    {
        ArgumentNullException.ThrowIfNull(taskGroups);
        if (taskGroups.Count is 0)
            return;

        var ownedTaskGroups = taskGroups.ToArray();
        var queuedTaskGroups = new HashSet<IDownloadTaskGroup>(ReferenceEqualityComparer.Instance);
        try
        {
            var entries = ownedTaskGroups
                .Select(static task => task.DatabaseEntry)
                .OfType<SubscriptionDownloadHistoryRecord>()
                .ToList();
            if (entries.Count != ownedTaskGroups.Length)
                throw new ArgumentException("A subscription download batch must only contain subscription history entries.", nameof(taskGroups));

            await Task.Run(() => StorageEngine.DownloadRepository.AddOrReplaceSubscriptionDownloadHistoryBatch(entries)).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                List<Exception>? exceptions = null;
                _isCommittingDownloadBatch = true;
                try
                {
                    foreach (var taskGroup in ownedTaskGroups)
                    {
                        try
                        {
                            DownloadManager.QueueTask(taskGroup);
                        }
                        catch (Exception exception)
                        {
                            (exceptions ??= []).Add(exception);
                        }
                        finally
                        {
                            if (DownloadManager.QueuedTasks.Any(queuedTask => ReferenceEquals(queuedTask, taskGroup)))
                                _ = queuedTaskGroups.Add(taskGroup);
                        }
                    }
                }
                finally
                {
                    _isCommittingDownloadBatch = false;
                }

                if (exceptions is not null)
                    throw new AggregateException("One or more committed subscription downloads could not be queued.", exceptions);
            });
        }
        finally
        {
            foreach (var taskGroup in ownedTaskGroups)
                if (!queuedTaskGroups.Contains(taskGroup))
                    taskGroup.Dispose();
        }
    }

    public async Task RemoveWorkSubscriptionDownloadsAsync(int workSubscriptionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workSubscriptionId);

        void RemoveQueuedTasks()
        {
            _ = _removedWorkSubscriptionIds.Add(workSubscriptionId);
            _isRemovingSubscriptionDownloads = true;
            try
            {
                foreach (var task in DownloadManager.QueuedTasks
                             .Where(task => task is IDownloadTaskGroup
                             {
                                 DatabaseEntry: SubscriptionDownloadHistoryRecord
                                 {
                                     WorkSubscriptionId: var id
                                 }
                             } && id == workSubscriptionId)
                             .ToArray())
                    _ = DownloadManager.TryRemoveTask(task);
            }
            finally
            {
                _isRemovingSubscriptionDownloads = false;
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
            RemoveQueuedTasks();
        else
            await Dispatcher.UIThread.InvokeAsync(RemoveQueuedTasks);

        await Task.Run(() =>
                StorageEngine.DownloadRepository.DeleteSubscriptionDownloadsByWorkSubscriptionId(workSubscriptionId))
            .ConfigureAwait(false);
    }

    private void OnSearchHistoryCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs args)
    {
        if (_isDisposed || _isRestoringSearchHistory || _isUpdatingSearchHistory)
            return;

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (args.NewItems is { } newItems)
                    foreach (var newItem in newItems.OfType<SearchHistoryRecord>())
                    {
                        _ = _removedSearchHistoryValues.Remove(newItem.Value);
                        StorageEngine.UpsertSearchHistory(newItem.Value, newItem.TranslatedName, DateTimeOffset.UtcNow.ToString("O"));
                    }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (args.OldItems is { } oldItems)
                    foreach (var oldItem in oldItems.OfType<SearchHistoryRecord>())
                    {
                        if (!_isSearchHistoryRestoreCompleted)
                            _ = _removedSearchHistoryValues.Add(oldItem.Value);
                        _ = StorageEngine.TryDeleteSearchHistoryByValue(oldItem.Value);
                    }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (args.OldItems is { } replacedItems)
                    foreach (var oldItem in replacedItems.OfType<SearchHistoryRecord>())
                        if (args.NewItems?.OfType<SearchHistoryRecord>().Any(newItem =>
                                newItem.Value == oldItem.Value) is not true)
                        {
                            if (!_isSearchHistoryRestoreCompleted)
                                _ = _removedSearchHistoryValues.Add(oldItem.Value);
                            _ = StorageEngine.TryDeleteSearchHistoryByValue(oldItem.Value);
                        }

                if (args.NewItems is { } replacementItems)
                    foreach (var newItem in replacementItems.OfType<SearchHistoryRecord>())
                    {
                        _ = _removedSearchHistoryValues.Remove(newItem.Value);
                        StorageEngine.UpsertSearchHistory(newItem.Value, newItem.TranslatedName, DateTimeOffset.UtcNow.ToString("O"));
                    }
                break;

            case NotifyCollectionChangedAction.Reset when args.NewItems is not { Count: > 0 }:
                _searchRestoreCancellationTokenSource.Cancel();
                StorageEngine.ClearSearchHistory();
                break;

            case NotifyCollectionChangedAction.Move:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(args.Action), args.Action, null);
        }
    }

    private void OnDownloadHistoryCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs args)
    {
        if (_isDisposed
            || _isRestoringDownloadHistory
            || _isCommittingDownloadBatch
            || _isRemovingSubscriptionDownloads)
            return;

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (args.NewItems is { } newItems)
                    foreach (var newItem in newItems.OfType<IDownloadTaskGroup>())
                    {
                        _ = _removedDownloadHistoryKeys.Remove(newItem.Key);
                        StorageEngine.DownloadRepository.AddOrReplace(newItem.DatabaseEntry);
                    }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (args.OldItems is { } oldItems)
                    foreach (var oldItem in oldItems.OfType<IDownloadTaskGroup>())
                    {
                        if (!_isDownloadHistoryRestoreCompleted)
                            _ = _removedDownloadHistoryKeys.Add(oldItem.Key);
                        _ = StorageEngine.DownloadRepository.TryDelete(oldItem.DatabaseEntry);
                    }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (args.NewItems is { } replacementItems)
                    foreach (var newItem in replacementItems.OfType<IDownloadTaskGroup>())
                    {
                        _ = _removedDownloadHistoryKeys.Remove(newItem.Key);
                        StorageEngine.DownloadRepository.AddOrReplace(newItem.DatabaseEntry);
                    }

                if (args.OldItems is { } replacedItems)
                    foreach (var oldItem in replacedItems.OfType<IDownloadTaskGroup>())
                        if (args.NewItems?.OfType<IDownloadTaskGroup>().Any(newItem =>
                                newItem.Key == oldItem.Key) is not true)
                        {
                            if (!_isDownloadHistoryRestoreCompleted)
                                _ = _removedDownloadHistoryKeys.Add(oldItem.Key);
                            _ = StorageEngine.DownloadRepository.TryDelete(oldItem.DatabaseEntry);
                        }
                break;

            case NotifyCollectionChangedAction.Reset when args.NewItems is not { Count: > 0 }:
                _downloadRestoreCancellationTokenSource.Cancel();
                StorageEngine.DownloadRepository.ClearDownloadHistory();
                StorageEngine.DownloadRepository.ClearSubscriptionDownloadHistory();
                break;

            case NotifyCollectionChangedAction.Move:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(args.Action), args.Action, null);
        }
    }

    private async Task RestoreSafelyAsync(
        Func<CancellationToken, Task> restoreAsync,
        string operationName,
        CancellationToken token)
    {
        try
        {
            await restoreAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            logger.LogError(operationName, e);
        }
    }

    private async Task RestoreSearchHistoryAsync(CancellationToken token)
    {
        try
        {
            long? cursorId = null;
            const uint pageSize = 50;
            while (!token.IsCancellationRequested)
            {
                var batch = StorageEngine.StreamSearchHistoriesCursor(cursorId, pageSize);
                if (batch.Count == 0)
                    break;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    token.ThrowIfCancellationRequested();
                    foreach (var entry in batch)
                    {
                        if (_removedSearchHistoryValues.Contains(entry.Value)
                            || SearchHistoryEntries.Any(item => item.Value == entry.Value))
                            continue;

                        _isRestoringSearchHistory = true;
                        try
                        {
                            SearchHistoryEntries.Add(entry);
                        }
                        finally
                        {
                            _isRestoringSearchHistory = false;
                        }
                    }
                });

                cursorId = batch[^1].HistoryEntryId;
                if (batch.Count < pageSize)
                    break;
            }
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _isSearchHistoryRestoreCompleted = true;
                _removedSearchHistoryValues.Clear();
            });
        }
    }

    private async Task RestoreDownloadHistoryAsync(CancellationToken token)
    {
        try
        {
            await Task.WhenAll(
                    RestoreRegularDownloadsAsync(token),
                    RestoreSubscriptionDownloadsAsync(token))
                .ConfigureAwait(false);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _isDownloadHistoryRestoreCompleted = true;
                _removedDownloadHistoryKeys.Clear();
            });
        }
    }

    private async Task RestoreRegularDownloadsAsync(CancellationToken token)
    {
        long? cursorId = null;
        const uint pageSize = 50;
        while (!token.IsCancellationRequested)
        {
            var records = StorageEngine.DownloadRepository.StreamDownloadHistoryCursor(cursorId, pageSize);
            if (records.Count == 0)
                break;

            foreach (var record in records)
            {
                token.ThrowIfCancellationRequested();
                if (record.Entry is null)
                    continue;

                var taskGroup = record.ToTaskGroup();
                var isOwnedByDownloadManager = false;
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        if (_removedDownloadHistoryKeys.Contains(taskGroup.Key))
                            return;

                        _isRestoringDownloadHistory = true;
                        try
                        {
                            isOwnedByDownloadManager = DownloadManager.TryRestoreTask(taskGroup);
                        }
                        finally
                        {
                            _isRestoringDownloadHistory = false;
                        }
                    });
                }
                finally
                {
                    if (!isOwnedByDownloadManager)
                        taskGroup.Dispose();
                }
            }

            cursorId = records[^1].HistoryEntryId;
            if (records.Count < pageSize)
                break;
        }
    }

    private async Task RestoreSubscriptionDownloadsAsync(CancellationToken token)
    {
        long? cursorId = null;
        const uint pageSize = 50;
        while (!token.IsCancellationRequested)
        {
            var records = StorageEngine.DownloadRepository.StreamSubscriptionDownloadHistoryCursor(cursorId, pageSize);
            if (records.Count == 0)
                break;

            foreach (var record in records)
            {
                token.ThrowIfCancellationRequested();
                if (record.Entry is null)
                    continue;

                var taskGroup = record.ToTaskGroup();
                var isOwnedByDownloadManager = false;
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        if (_removedWorkSubscriptionIds.Contains((int)record.WorkSubscriptionId))
                            return;
                        if (_removedDownloadHistoryKeys.Contains(taskGroup.Key))
                            return;

                        _isRestoringDownloadHistory = true;
                        try
                        {
                            isOwnedByDownloadManager = DownloadManager.TryRestoreTask(taskGroup);
                        }
                        finally
                        {
                            _isRestoringDownloadHistory = false;
                        }
                    });
                }
                finally
                {
                    if (!isOwnedByDownloadManager)
                        taskGroup.Dispose();
                }
            }

            cursorId = records[^1].HistoryEntryId;
            if (records.Count < pageSize)
                break;
        }
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
                var entry = StorageEngine.UpsertLoginUser(LoginUserRecord.FromTokenUser(tokenResponse.RefreshToken, user));
                LoginContext.CurrentKey = (int)entry.HistoryEntryId;
            }
        }

        void Notify()
        {
            PixevalSettings.Instance.OnIsLoggedInChanged();
            UserRefreshed?.Invoke(user);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Notify();
        else
            Dispatcher.UIThread.Post(Notify);

        AppInfo.SaveLoginContext(LoginContext);
    }

    public event Action<TokenUser?>? UserRefreshed;

    public LoginUserRecord? GetCurrentLoginUser()
    {
        return StorageEngine.GetLoginUserByKey(LoginContext.CurrentKey);
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
        var config = AppSettings.ToMakoConfiguration();
        MakoClient.UpdateConfiguration(config);
        CacheHelper.UpdateNetworkOptions(config);
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
        if (_isDisposed)
            return;
        _isDisposed = true;
        try
        {
            _searchRestoreCancellationTokenSource.Cancel();
            _downloadRestoreCancellationTokenSource.Cancel();
            SearchHistoryEntries.CollectionChanged -= OnSearchHistoryCollectionChanged;
            if (DownloadManager is not null)
            {
                DownloadManager.QueuedTasks.CollectionChanged -= OnDownloadHistoryCollectionChanged;
                DownloadManager.Dispose();
            }
            _searchRestoreCancellationTokenSource.Dispose();
            _downloadRestoreCancellationTokenSource.Dispose();

            if (AppServiceProvider?.GetService<WorkSubscriptionDownloadService>() is { } subscriptionService)
                await subscriptionService.CancelAndWaitAsync();

            if (AppServiceProvider is not null)
                await AppServiceProvider.DisposeAsync();

            MahoTransport.Dispose();
            MakoClient.Dispose();
            StorageEngine.Dispose();
        }
        catch
        {
            // ignored
            // 保证退出时不出幺蛾子
        }
    }
}
