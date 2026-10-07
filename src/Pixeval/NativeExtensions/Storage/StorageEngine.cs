// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Threading;

namespace Pixeval.Native.Storage;

public partial class StorageEngine
{
    private StorageObserverBridge? _observerBridge;
    private HistoryRepository? _historyRepo;
    private WatchLaterRepository? _watchLaterRepo;
    private DownloadRepository? _downloadRepo;

    public HistoryRepository HistoryRepository => _historyRepo ??= History();
    public WatchLaterRepository WatchLaterRepository => _watchLaterRepo ??= WatchLater();
    public DownloadRepository DownloadRepository => _downloadRepo ??= Download();

    public BlockedUserRecord AddOrUpdateBlockedUser(BlockedUserRecord record) =>
        AddOrUpdateBlockedUser(record.Id, record.UserName, record.AvatarUrl, record.Account);

    public WorkSubscriptionRecord UpsertSubscription(WorkSubscriptionRecord record) =>
        UpsertSubscription(
            record.Id,
            record.SubscriptionType,
            record.WorkKind,
            record.Title,
            record.Author,
            record.Avatar,
            record.LastCheckTime,
            record.LastWorkId);

    public WorkSubscriptionRecord? GetSubscriptionByIdentity(long id, uint subscriptionType, uint workKind) =>
        GetSubscriptionByKey(id, subscriptionType, workKind);

    public void InitializeObserver()
    {
        if (_observerBridge != null)
            return;

        _observerBridge = new StorageObserverBridge(this);
        RegisterObserver(_observerBridge);
    }

    public event EventHandler? BrowseHistoryChanged;
    public event EventHandler? WatchLaterChanged;
    public event EventHandler? DownloadHistoryChanged;
    public event EventHandler? SearchHistoryChanged;

    private sealed class StorageObserverBridge(StorageEngine engine) : IStorageObserver
    {
        public void OnBrowseHistoryChanged()
        {
            Dispatcher.UIThread.Post(() =>
            {
                engine.HistoryRepository.NotifyChanged();
                engine.BrowseHistoryChanged?.Invoke(engine, EventArgs.Empty);
            });
        }

        public void OnWatchLaterChanged()
        {
            Dispatcher.UIThread.Post(() =>
            {
                engine.WatchLaterRepository.NotifyChanged();
                engine.WatchLaterChanged?.Invoke(engine, EventArgs.Empty);
            });
        }

        public void OnDownloadHistoryChanged()
        {
            Dispatcher.UIThread.Post(() =>
            {
                engine.DownloadHistoryChanged?.Invoke(engine, EventArgs.Empty);
            });
        }

        public void OnSearchHistoryChanged()
        {
            Dispatcher.UIThread.Post(() =>
            {
                engine.SearchHistoryChanged?.Invoke(engine, EventArgs.Empty);
            });
        }
    }
}
