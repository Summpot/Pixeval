using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Imouto.BooruParser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misaki;
using Pixeval.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Native.Download;
using Pixeval.Models.Options;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Storage;
using Pixeval.Native.Subscription;
using Pixeval.ViewModels;

namespace Pixeval.Tests;

[TestClass]
public sealed class DownloadManagerTest
{
    [TestMethod]
    public void QueueTask_ReplacesViewModelAndStaleRemovalRemovesReplacement()
    {
        using var httpClient = new HttpClient();
        using var manager = new DownloadManager(httpClient, 1);
        using var storage = new StorageEngine(":memory:");
        using var viewModel = new DownloadPageViewModel(
            manager.QueuedTasks,
            storage,
            new TestWorkSubscriptionService());
        var original = new TestDownloadTaskGroup("same", "1");
        var other = new TestDownloadTaskGroup("other", "2");
        var replacement = new TestDownloadTaskGroup("same", "3");

        manager.QueueTask(original);
        manager.QueueTask(other);
        manager.QueueTask(replacement);

        Assert.HasCount(2, manager.QueuedTasks);
        Assert.IsTrue(original.IsCancelled);
        Assert.AreSame(replacement, manager.QueuedTasks[0]);
        Assert.HasCount(2, viewModel.OrdinaryItems);
        Assert.AreSame(replacement, viewModel.OrdinaryItems[0].DownloadTask);

        Assert.IsTrue(manager.TryRemoveTask(original));
        Assert.IsTrue(replacement.IsCancelled);
        Assert.HasCount(1, manager.QueuedTasks);
        Assert.AreSame(other, manager.QueuedTasks[0]);
        Assert.HasCount(1, viewModel.OrdinaryItems);
        Assert.AreSame(other, viewModel.OrdinaryItems[0].DownloadTask);
        Assert.IsFalse(manager.TryRemoveTask(original));
    }

    [TestMethod]
    public void QueueTask_SubscriptionIdentityAllowsSameDestinationToCoexist()
    {
        using var httpClient = new HttpClient();
        using var manager = new DownloadManager(httpClient, 1);
        var first = new TestDownloadTaskGroup("same", "artwork", 1);
        var otherSubscription = new TestDownloadTaskGroup("same", "artwork", 2);
        var otherArtwork = new TestDownloadTaskGroup("same", "other", 1);
        var replacement = new TestDownloadTaskGroup("same", "artwork", 1);

        manager.QueueTask(first);
        manager.QueueTask(otherSubscription);
        manager.QueueTask(otherArtwork);
        manager.QueueTask(replacement);

        Assert.HasCount(3, manager.QueuedTasks);
        Assert.AreSame(replacement, manager.QueuedTasks[0]);
        Assert.Contains(otherSubscription, manager.QueuedTasks);
        Assert.Contains(otherArtwork, manager.QueuedTasks);
    }

    [TestMethod]
    public async Task ViewModel_ProjectsOrdinaryAndSubscriptionSources()
    {
        using var httpClient = new HttpClient();
        using var manager = new DownloadManager(httpClient, 1);
        using var storage = new StorageEngine(":memory:");
        var subscription = storage.UpsertSubscription(
            1,
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            "Subscription",
            "author",
            "",
            "",
            null);
        using var viewModel = new DownloadPageViewModel(
            manager.QueuedTasks,
            storage,
            new TestWorkSubscriptionService());
        await viewModel.SubscriptionFoldersLoadTask;
        var ordinary = new TestDownloadTaskGroup("ordinary", "ordinary");
        var subscriptionTask = new TestDownloadTaskGroup(
            "subscription",
            "subscription",
            (int)subscription.HistoryEntryId);

        manager.QueueTask(ordinary);
        manager.QueueTask(subscriptionTask);

        Assert.HasCount(1, viewModel.OrdinaryItems);
        Assert.AreSame(ordinary, viewModel.OrdinaryItems[0].DownloadTask);
        Assert.HasCount(1, viewModel.SubscriptionFolders);
        var folder = viewModel.SubscriptionFolders[0];
        Assert.HasCount(1, folder.Items);
        Assert.AreSame(subscriptionTask, folder.Items[0].DownloadTask);

        Assert.HasCount(1, viewModel.OrdinaryItems);
        Assert.AreSame(ordinary, viewModel.OrdinaryItems[0].DownloadTask);

        Assert.IsTrue(manager.TryRemoveTask(subscriptionTask));
        Assert.HasCount(1, viewModel.SubscriptionFolders);
        Assert.IsEmpty(viewModel.SubscriptionFolders[0].Items);
    }

    [TestMethod]
    public async Task ViewModel_PreservesSubscriptionSourceOrderDuringInitialProjection()
    {
        using var storage = new StorageEngine(":memory:");
        var subscription = storage.UpsertSubscription(
            1,
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            "Subscription",
            "author",
            "",
            "",
            null);
        var newest = new TestDownloadTaskGroup("newest", "newest", (int)subscription.HistoryEntryId);
        var older = new TestDownloadTaskGroup("older", "older", (int)subscription.HistoryEntryId);
        ObservableCollection<IDownloadTaskGroupBase> source = [newest, older];
        using var viewModel = new DownloadPageViewModel(
            source,
            storage,
            new TestWorkSubscriptionService());
        await viewModel.SubscriptionFoldersLoadTask;

        var folder = viewModel.SubscriptionFolders[0];
        Assert.AreSame(newest, folder.Items[0].DownloadTask);
        Assert.AreSame(older, folder.Items[1].DownloadTask);
    }

    [TestMethod]
    public async Task ViewModel_DisplaysSubscriptionsWithoutTasks()
    {
        using var storage = new StorageEngine(":memory:");
        _ = storage.UpsertSubscription(
            1,
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            "Subscription",
            "author",
            "",
            "",
            null);
        using var httpClient = new HttpClient();
        using var manager = new DownloadManager(httpClient, 1);
        using var viewModel = new DownloadPageViewModel(
            manager.QueuedTasks,
            storage,
            new TestWorkSubscriptionService());

        await viewModel.SubscriptionFoldersLoadTask;

        Assert.HasCount(1, viewModel.SubscriptionFolders);
        Assert.IsEmpty(viewModel.SubscriptionFolders[0].Items);
    }

    [TestMethod]
    public async Task ViewModel_TracksSubscriptionFetchState()
    {
        using var storage = new StorageEngine(":memory:");
        var subscription = storage.UpsertSubscription(
            1,
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            "Subscription",
            "author",
            "",
            "",
            null);
        using var httpClient = new HttpClient();
        using var manager = new DownloadManager(httpClient, 1);
        var fetchStateSource = new TestWorkSubscriptionService();
        fetchStateSource.Update(new(subscription.HistoryEntryId, 1, 42, SubscriptionStatus.Fetching));
        using var viewModel = new DownloadPageViewModel(
            manager.QueuedTasks,
            storage,
            fetchStateSource);

        await viewModel.SubscriptionFoldersLoadTask;

        var folder = viewModel.SubscriptionFolders[0];
        Assert.IsTrue(folder.IsFetching);
        Assert.AreEqual(42, folder.FetchedCount);

        fetchStateSource.Update(new(subscription.HistoryEntryId, 1, 42, SubscriptionStatus.Completed));

        Assert.IsFalse(folder.IsFetching);
        Assert.AreEqual(0, folder.FetchedCount);
    }

    [TestMethod]
    public async Task ViewModel_AddsNewSubscriptionWhenFetchingStarts()
    {
        using var storage = new StorageEngine(":memory:");
        using var httpClient = new HttpClient();
        using var manager = new DownloadManager(httpClient, 1);
        var fetchStateSource = new TestWorkSubscriptionService();
        using var viewModel = new DownloadPageViewModel(
            manager.QueuedTasks,
            storage,
            fetchStateSource);

        await viewModel.SubscriptionFoldersLoadTask;
        Assert.IsEmpty(viewModel.SubscriptionFolders);

        var subscription = storage.UpsertSubscription(
            1,
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            "Subscription",
            "author",
            "",
            "",
            null);
        fetchStateSource.Update(new(subscription.HistoryEntryId, 1, 0, SubscriptionStatus.Fetching));

        Assert.HasCount(1, viewModel.SubscriptionFolders);
        Assert.AreEqual(
            subscription.HistoryEntryId,
            viewModel.SubscriptionFolders[0].Subscription.HistoryEntryId);
        Assert.IsTrue(viewModel.SubscriptionFolders[0].IsFetching);
    }

    [TestMethod]
    public async Task ViewModel_RemovesFolderWhenSubscriptionIsRemoved()
    {
        using var storage = new StorageEngine(":memory:");
        var subscription = storage.UpsertSubscription(
            1,
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            "Subscription",
            "author",
            "",
            "",
            null);
        using var httpClient = new HttpClient();
        using var manager = new DownloadManager(httpClient, 1);
        var subscriptionService = new TestWorkSubscriptionService();
        using var viewModel = new DownloadPageViewModel(
            manager.QueuedTasks,
            storage,
            subscriptionService);
        await viewModel.SubscriptionFoldersLoadTask;
        Assert.HasCount(1, viewModel.SubscriptionFolders);

        _ = storage.DeleteSubscription(subscription.HistoryEntryId);
        subscriptionService.Remove((int)subscription.HistoryEntryId);

        Assert.IsEmpty(viewModel.SubscriptionFolders);
    }

    [TestMethod]
    public async Task ViewModel_UpdatesSubscriptionMetadata()
    {
        using var storage = new StorageEngine(":memory:");
        var subscription = storage.UpsertSubscription(
            1,
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            "Old",
            "author",
            "old-avatar",
            "",
            null);
        using var httpClient = new HttpClient();
        using var manager = new DownloadManager(httpClient, 1);
        var subscriptionService = new TestWorkSubscriptionService();
        using var viewModel = new DownloadPageViewModel(
            manager.QueuedTasks,
            storage,
            subscriptionService);
        await viewModel.SubscriptionFoldersLoadTask;
        var folder = viewModel.SubscriptionFolders[0];
        var changedProperties = new List<string?>();
        folder.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        subscriptionService.UpdateSubscription(subscription with
        {
            Title = "New",
            Avatar = "new-avatar"
        });

        Assert.AreEqual("New", folder.Subscription.Name);
        Assert.AreEqual("new-avatar", folder.Subscription.AvatarUrl);
        CollectionAssert.Contains(changedProperties, nameof(DownloadFolderViewModel.Title));
        CollectionAssert.Contains(changedProperties, nameof(DownloadFolderViewModel.Subscription));
    }

    private sealed class TestWorkSubscriptionService : IWorkSubscriptionService
    {
        public SubscriptionFetchState? CurrentFetchState { get; private set; }

        public event EventHandler<SubscriptionFetchState>? FetchStateChanged;

        public event EventHandler<WorkSubscriptionRecord>? SubscriptionUpdated;

        public event EventHandler<long>? SubscriptionRemoved;

        public WorkSubscriptionType? LastQueryType { get; private set; }

        public WorkSubscriptionRecord? TryGetSubscription(
            long targetId,
            WorkSubscriptionType subscriptionType,
            WorkSubscriptionWorkKind workKind) => null;

        public Task<WorkSubscriptionRecord?> TryRemoveAsync(long historyEntryId) =>
            Task.FromResult<WorkSubscriptionRecord?>(null);

        public void Update(SubscriptionFetchState state)
        {
            CurrentFetchState = state.Status == SubscriptionStatus.Fetching ? state : null;
            FetchStateChanged?.Invoke(this, state);
        }

        public void Remove(long workSubscriptionId) =>
            SubscriptionRemoved?.Invoke(this, workSubscriptionId);

        public void UpdateSubscription(WorkSubscriptionRecord subscription) =>
            SubscriptionUpdated?.Invoke(this, subscription);
    }

    private sealed class TestDownloadTaskGroup(
        string destination,
        string id,
        int? workSubscriptionId = null) : IDownloadTaskGroup
    {
        public IDownloadHistoryEntry DatabaseEntry { get; } =
            IDownloadHistoryEntry.Create(destination, CreatePost(id), workSubscriptionId);

        public string Id => DatabaseEntry.Entry?.Id ?? "";

        public double ProgressPercentage => 100;

        public DownloadState CurrentState => DownloadState.Completed;

        public string Destination => DatabaseEntry.Destination;

        public string? ErrorMessage => null;

        public string OpenLocalDestination => Destination;

        public bool IsProcessing => false;

        public int ActiveCount => 0;

        public int CompletedCount => 1;

        public int ErrorCount => 0;

        public int Count => 0;

        public bool IsCancelled { get; private set; }

        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add { }
            remove { }
        }

        event PropertyChangingEventHandler? INotifyPropertyChanging.PropertyChanging
        {
            add { }
            remove { }
        }

        public ValueTask InitializeTaskGroupAsync() => ValueTask.CompletedTask;

        public void Reset()
        {
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Cancel() => IsCancelled = true;

        public void Delete()
        {
        }

        public void Dispose()
        {
        }

        public IEnumerator<ISingleDownloadTaskBase> GetEnumerator() =>
            ((IEnumerable<ISingleDownloadTaskBase>) []).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static Post CreatePost(string id) => new(
        new(id, $"hash-{id}", PlatformType.Danbooru),
        $"https://example.com/{id}.jpg",
        null,
        null,
        ExistState.Exist,
        DateTimeOffset.UtcNow,
        new("1", "uploader", PlatformType.Danbooru),
        null,
        new(100, 100),
        0,
        SafeRating.General,
        [],
        null);
}
