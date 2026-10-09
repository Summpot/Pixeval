// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Misaki;
using Pixeval.AppManagement;
using Pixeval.Models.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Options;
using Pixeval.Native.Download;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Native.Subscription;
using Pixeval.Utilities;
using Pixeval.Views;

namespace Pixeval.Models.Subscriptions;

public sealed class WorkSubscriptionDownloadService : IWorkSubscriptionService, ISubscriptionProgressCallback, IAsyncDisposable
{
    private readonly StorageEngine _storageEngine;
    private readonly MakoClient _makoClient;
    private readonly FileLogger _logger;
    private readonly SubscriptionSyncEngine _syncEngine;
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private SubscriptionFetchState? _currentFetchState;
    private bool _isDisposed;

    public SubscriptionFetchState? CurrentFetchState
    {
        get
        {
            lock (_gate)
                return _currentFetchState;
        }
    }

    public event EventHandler<SubscriptionFetchState>? FetchStateChanged;

    public event EventHandler<WorkSubscriptionRecord>? SubscriptionUpdated;

    public event EventHandler<long>? SubscriptionRemoved;

    public event EventHandler<uint>? NewWorksIngested;

    public event EventHandler<bool>? DaemonStateChanged;

    public bool IsSyncInProgress => _syncEngine.IsSyncInProgress();

    public SubscriptionSyncEngine SyncEngine => _syncEngine;

    public bool IsDaemonRunning => _syncEngine.IsDaemonRunning();

    public ulong DaemonIntervalSecs => _syncEngine.GetDaemonInterval();

    public WorkSubscriptionDownloadService(
        StorageEngine storageEngine,
        DownloadManager downloadManager,
        MakoClient makoClient,
        FileLogger logger)
    {
        _storageEngine = storageEngine;
        _makoClient = makoClient;
        _logger = logger;

        var config = CreateSyncConfig();
        _syncEngine = SubscriptionSyncEngine.NewWithServices(
            storageEngine,
            makoClient,
            downloadManager,
            config,
            this);

        var settings = App.AppViewModel?.AppSettings?.DownloadSettings;
        if (settings?.EnableSubscriptionDaemon ?? true)
        {
            var intervalMinutes = Math.Max(1, settings?.SubscriptionDaemonIntervalMinutes ?? 30);
            _syncEngine.StartDaemon((ulong)(intervalMinutes * 60));
        }
    }

    public void StartDaemon(ulong intervalSecs = 1800)
    {
        lock (_gate)
        {
            if (_isDisposed)
                return;
            _syncEngine.StartDaemon(intervalSecs);
        }
    }

    public void StopDaemon()
    {
        lock (_gate)
        {
            _syncEngine.StopDaemon();
        }
    }

    public void SetDaemonInterval(ulong intervalSecs)
    {
        lock (_gate)
        {
            _syncEngine.SetDaemonInterval(intervalSecs);
        }
    }

    public void QueueSyncAll()
    {
        lock (_gate)
        {
            if (_isDisposed)
                return;
            _syncEngine.UpdateConfig(CreateSyncConfig());
            _ = _syncEngine.QueueSyncAll();
        }
    }

    public void QueueSyncSubscription(WorkSubscriptionRecord subscription)
    {
        lock (_gate)
        {
            if (_isDisposed)
                return;
            _syncEngine.UpdateConfig(CreateSyncConfig());
            _ = _syncEngine.QueueSyncSubscription(subscription.HistoryEntryId);
        }
    }

    public void QueueInitialSync(
        WorkSubscriptionRecord subscription,
        IFetchEngine<IWorkEntry>? sourceEngine = null) =>
        QueueSyncSubscription(subscription);

    public void QueueSyncCurrentSource(
        long targetId,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind,
        IFetchEngine<IWorkEntry> engine)
    {
        if (TryGetSubscription(targetId, subscriptionType, workKind) is { } subscription)
            QueueSyncSubscription(subscription);
    }

    public WorkSubscriptionRecord? TryGetSubscription(
        long targetId,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind) =>
        _storageEngine.GetSubscriptionByIdentity(targetId, (uint)subscriptionType, (uint)workKind);

    public async Task<WorkSubscriptionRecord?> TryRemoveAsync(long historyEntryId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(historyEntryId);
        WorkSubscriptionRecord? subscription;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            subscription = _storageEngine.GetSubscriptionByHistoryId(historyEntryId);
            if (subscription is null)
                return null;

            _syncEngine.RemovePendingSubscription(historyEntryId);
        }

        var wasDeleted = false;
        await _mutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_storageEngine.GetSubscriptionByHistoryId(historyEntryId) is not { } persistedSubscription
                || !_storageEngine.DeleteSubscription(historyEntryId))
                return null;

            subscription = persistedSubscription;
            wasDeleted = true;
            if (App.AppViewModel is { } app)
                await app.RemoveWorkSubscriptionDownloadsAsync((int)historyEntryId).ConfigureAwait(false);
            return subscription;
        }
        finally
        {
            _mutationGate.Release();
            if (wasDeleted)
                NotifySubscriptionRemoved(historyEntryId);
        }
    }

    public async Task CancelAndWaitAsync()
    {
        lock (_gate)
        {
            _syncEngine.CancelAll();
        }

        while (_syncEngine.IsSyncInProgress())
        {
            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
        }

        _syncEngine.StopDaemon();
        await CancelAndWaitAsync().ConfigureAwait(false);
        _syncEngine.Dispose();
        GC.SuppressFinalize(this);
    }

    public void OnFetchStateChanged(SubscriptionFetchState state)
    {
        var isFetching = state.Status == SubscriptionStatus.Fetching;

        lock (_gate)
        {
            _currentFetchState = isFetching ? state : null;
        }

        try
        {
            void Notify() => FetchStateChanged?.Invoke(this, state);
            if (Dispatcher.UIThread.CheckAccess())
                Notify();
            else
                Dispatcher.UIThread.Post(Notify);
        }
        catch (Exception ex)
        {
            _logger.LogError(nameof(OnFetchStateChanged), ex);
        }
    }

    public void OnSubscriptionUpdated(long subscriptionId, string name, string account, string avatarUrl)
    {
        try
        {
            if (_storageEngine.GetSubscriptionByHistoryId(subscriptionId) is { } entry)
            {
                var updated = entry with { Title = name, Author = account, Avatar = avatarUrl };
                _storageEngine.UpsertSubscription(updated);

                void Notify() => SubscriptionUpdated?.Invoke(this, updated);
                if (Dispatcher.UIThread.CheckAccess())
                    Notify();
                else
                    Dispatcher.UIThread.Post(Notify);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(nameof(OnSubscriptionUpdated), ex);
        }
    }

    public void OnItemFetched(SubscriptionDownloadItem item)
    {
        try
        {
            var macro = App.AppViewModel?.AppSettings?.DownloadSettings?.DownloadPathMacro ?? string.Empty;
            var sub = _storageEngine.GetSubscriptionByHistoryId(item.WorkSubscriptionId);
            IDownloadTaskGroup task;
            if (item.IsNovel)
            {
                var novel = Novel.Deserialize(item.PayloadJson);
                task = new NovelDownloadTaskFactory().Create(new ParserContext(novel, sub), macro, null);
            }
            else
            {
                var illust = Illustration.Deserialize(item.PayloadJson);
                task = new IllustrationDownloadTaskFactory().Create(new ParserContext(illust, sub), macro);
            }
            if (App.AppViewModel is { } app)
                _ = app.QueueSubscriptionDownloadBatchAsync([task]);
        }
        catch (Exception ex)
        {
            _logger.LogError(nameof(OnItemFetched), ex);
        }
    }

    public void OnDuplicateStopped(long subscriptionId, uint duplicateCount)
    {
    }

    public void OnSyncFinished()
    {
        lock (_gate)
        {
            _currentFetchState = null;
        }
    }

    public void OnNewWorksIngested(uint totalCount)
    {
        try
        {
            void Notify() => NewWorksIngested?.Invoke(this, totalCount);
            if (Dispatcher.UIThread.CheckAccess())
                Notify();
            else
                Dispatcher.UIThread.Post(Notify);
        }
        catch (Exception ex)
        {
            _logger.LogError(nameof(OnNewWorksIngested), ex);
        }
    }

    public void OnDaemonStateChanged(bool isRunning)
    {
        try
        {
            void Notify() => DaemonStateChanged?.Invoke(this, isRunning);
            if (Dispatcher.UIThread.CheckAccess())
                Notify();
            else
                Dispatcher.UIThread.Post(Notify);
        }
        catch (Exception ex)
        {
            _logger.LogError(nameof(OnDaemonStateChanged), ex);
        }
    }

    private void NotifySubscriptionRemoved(long workSubscriptionId)
    {
        try
        {
            void Notify() => SubscriptionRemoved?.Invoke(this, workSubscriptionId);
            if (Dispatcher.UIThread.CheckAccess())
                Notify();
            else
                Dispatcher.UIThread.Post(Notify);
        }
        catch (Exception exception)
        {
            _logger.LogError(nameof(NotifySubscriptionRemoved), exception);
        }
    }

    private static SubscriptionSyncConfig CreateSyncConfig()
    {
        var settings = App.AppViewModel?.AppSettings;
        var macro = settings?.DownloadSettings.DownloadPathMacro ?? string.Empty;
        var overwrite = settings?.DownloadSettings.OverwriteDownloadedFile ?? false;
        long? myId = PixevalSettings.MyId == 0 ? null : PixevalSettings.MyId;
        return new SubscriptionSyncConfig(
            macro,
            string.Empty,
            overwrite,
            5,
            myId
        );
    }
}
