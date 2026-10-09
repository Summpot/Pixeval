// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Pixeval.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Storage;

namespace Pixeval.ViewModels;

public sealed class DownloadPageViewModel : ViewModelBase, IDisposable
{
    private readonly ObservableCollection<IDownloadTaskGroupBase> _source;
    private readonly StorageEngine _storageEngine;
    private readonly IWorkSubscriptionService _workSubscriptionService;
    private bool _isDisposed;

    private readonly bool _createdOnUiThread = Dispatcher.UIThread.CheckAccess();

    public ObservableCollection<DownloadItemViewModel> OrdinaryItems { get; } = [];
    public ObservableCollection<DownloadFolderViewModel> SubscriptionFolders { get; } = [];
    public Task SubscriptionFoldersLoadTask { get; } = Task.CompletedTask;

    public DownloadPageViewModel(
        ObservableCollection<IDownloadTaskGroupBase> source,
        StorageEngine storageEngine,
        IWorkSubscriptionService workSubscriptionService)
    {
        _source = source;
        _storageEngine = storageEngine;
        _workSubscriptionService = workSubscriptionService;

        foreach (var sub in _storageEngine.GetAllSubscriptions().OrderByDescending(s => s.HistoryEntryId))
        {
            var folder = new DownloadFolderViewModel(sub);
            folder.UpdateFetchState(_workSubscriptionService.CurrentFetchState);
            SubscriptionFolders.Add(folder);
        }

        foreach (var task in _source)
            AddTask(task, false);

        _source.CollectionChanged += SourceOnCollectionChanged;
        _workSubscriptionService.FetchStateChanged += OnFetchStateChanged;
        _workSubscriptionService.SubscriptionUpdated += OnSubscriptionUpdated;
        _workSubscriptionService.SubscriptionRemoved += OnSubscriptionRemoved;
    }

    private void RunOnUiThread(Action action)
    {
        if (_isDisposed)
            return;

        if (!_createdOnUiThread || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!_isDisposed)
                action();
        });
    }

    private DownloadFolderViewModel? GetFolder(long id) =>
        SubscriptionFolders.FirstOrDefault(f => f.Subscription.HistoryEntryId == id);

    private DownloadFolderViewModel AddSubscriptionFolder(WorkSubscriptionRecord subscription)
    {
        var folder = new DownloadFolderViewModel(subscription);
        folder.UpdateFetchState(_workSubscriptionService.CurrentFetchState);
        SubscriptionFolders.Insert(0, folder);
        return folder;
    }

    private void MoveTaskToFront(IDownloadTaskGroupBase task)
    {
        if (task is not IDownloadTaskGroup group)
            return;

        if (OrdinaryItems.FirstOrDefault(i => i.DownloadTask.Key == group.Key) is { } ordinary)
        {
            var idx = OrdinaryItems.IndexOf(ordinary);
            if (idx > 0)
                OrdinaryItems.Move(idx, 0);
        }

        foreach (var folder in SubscriptionFolders)
        {
            if (folder.Items.FirstOrDefault(i => i.DownloadTask.Key == group.Key) is { } item)
            {
                var idx = folder.Items.IndexOf(item);
                if (idx > 0)
                    folder.Items.Move(idx, 0);
            }
        }
    }

    private void AddTask(IDownloadTaskGroupBase task, bool insertAtFront)
    {
        if (_isDisposed || task is not IDownloadTaskGroup group)
            return;

        if (group.DatabaseEntry is SubscriptionDownloadHistoryRecord subEntry)
        {
            var folder = GetFolder(subEntry.WorkSubscriptionId)
                ?? (_storageEngine.GetSubscriptionByHistoryId(subEntry.WorkSubscriptionId) is { } sub
                    ? AddSubscriptionFolder(sub)
                    : null);

            if (folder is not null)
            {
                if (folder.Items.FirstOrDefault(i => i.DownloadTask.Key == group.Key) is { } existing)
                {
                    folder.Remove(existing);
                    existing.Dispose();
                }
                folder.Add(new DownloadItemViewModel(group), insertAtFront);
            }
        }
        else
        {
            if (OrdinaryItems.FirstOrDefault(i => i.DownloadTask.Key == group.Key) is { } existing)
            {
                OrdinaryItems.Remove(existing);
                existing.Dispose();
            }
            var vm = new DownloadItemViewModel(group);
            if (insertAtFront)
                OrdinaryItems.Insert(0, vm);
            else
                OrdinaryItems.Add(vm);
        }
    }

    private void RemoveTask(IDownloadTaskGroupBase task)
    {
        if (_isDisposed || task is not IDownloadTaskGroup group) return;
        if (OrdinaryItems.FirstOrDefault(i => i.DownloadTask.Key == group.Key) is { } ordinary)
        {
            OrdinaryItems.Remove(ordinary);
            ordinary.Dispose();
        }
        foreach (var folder in SubscriptionFolders)
        {
            if (folder.Items.FirstOrDefault(i => i.DownloadTask.Key == group.Key) is { } item)
            {
                folder.Remove(item);
                item.Dispose();
            }
        }
    }

    private void SourceOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isDisposed) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is { } added:
                foreach (IDownloadTaskGroupBase item in added)
                {
                    AddTask(item, e.NewStartingIndex is 0);
                    if (e.NewStartingIndex is 0)
                        MoveTaskToFront(item);
                }
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems is { } removed:
                foreach (IDownloadTaskGroupBase item in removed)
                    RemoveTask(item);
                break;

            case NotifyCollectionChangedAction.Replace when e is { OldItems: { } replaced, NewItems: { } replacements }:
                foreach (IDownloadTaskGroupBase item in replaced)
                    RemoveTask(item);
                foreach (IDownloadTaskGroupBase item in replacements)
                {
                    AddTask(item, e.NewStartingIndex is 0);
                    if (e.NewStartingIndex is 0)
                        MoveTaskToFront(item);
                }
                break;

            case NotifyCollectionChangedAction.Move when e.NewItems is { } moved:
                if (e.NewStartingIndex is 0)
                {
                    foreach (IDownloadTaskGroupBase item in moved)
                        MoveTaskToFront(item);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                foreach (var item in OrdinaryItems) item.Dispose();
                OrdinaryItems.Clear();
                foreach (var folder in SubscriptionFolders)
                {
                    foreach (var item in folder.Items.ToArray()) { folder.Remove(item); item.Dispose(); }
                }
                foreach (var task in _source) AddTask(task, false);
                break;
        }
    }

    private void OnFetchStateChanged(object? sender, SubscriptionFetchState state) =>
        RunOnUiThread(() =>
        {
            var folder = state.IsFetching
                ? (GetFolder(state.WorkSubscriptionId)
                    ?? (_storageEngine.GetSubscriptionByHistoryId(state.WorkSubscriptionId) is { } sub
                        ? AddSubscriptionFolder(sub)
                        : null))
                : GetFolder(state.WorkSubscriptionId);
            folder?.UpdateFetchState(state);
        });

    private void OnSubscriptionUpdated(object? sender, WorkSubscriptionRecord sub) =>
        RunOnUiThread(() => GetFolder(sub.HistoryEntryId)?.UpdateSubscription(sub));

    private void OnSubscriptionRemoved(object? sender, long id) =>
        RunOnUiThread(() =>
        {
            if (GetFolder(id) is { } folder)
            {
                SubscriptionFolders.Remove(folder);
                folder.Dispose();
            }
        });

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _source.CollectionChanged -= SourceOnCollectionChanged;
        _workSubscriptionService.FetchStateChanged -= OnFetchStateChanged;
        _workSubscriptionService.SubscriptionUpdated -= OnSubscriptionUpdated;
        _workSubscriptionService.SubscriptionRemoved -= OnSubscriptionRemoved;
        foreach (var item in OrdinaryItems) item.Dispose();
        OrdinaryItems.Clear();
        foreach (var folder in SubscriptionFolders) folder.Dispose();
        SubscriptionFolders.Clear();
    }
}
