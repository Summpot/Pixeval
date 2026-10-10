using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Models.Options;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Download;
using Pixeval.Native.Storage;
using Pixeval.ViewModels;

namespace Pixeval.Tests;

[TestClass]
public sealed class DownloadManagerTest
{
    [TestMethod]
    public void ApplyPageSnapshot_KeepsEqualItemsAndReplacesChanges()
    {
        using var manager = new DownloadManager(null, 1);
        var kept = Item("kept", "1", DownloadState.Running, 10);
        var removed = Item("removed", "2", DownloadState.Queued);
        manager.ApplyPageSnapshot(Page([kept, removed], []));

        var keptAgain = Item("kept", "1", DownloadState.Running, 10);
        var inserted = Item("inserted", "3", DownloadState.Completed, 100);
        manager.ApplyPageSnapshot(Page([inserted, keptAgain], []));

        Assert.HasCount(2, manager.OrdinaryItems);
        Assert.AreSame(inserted, manager.OrdinaryItems[0]);
        Assert.AreSame(kept, manager.OrdinaryItems[1]);

        var progressed = Item("kept", "1", DownloadState.Running, 40);
        manager.ApplyPageSnapshot(Page([inserted, progressed], []));

        Assert.AreSame(inserted, manager.OrdinaryItems[0]);
        Assert.AreNotSame(kept, manager.OrdinaryItems[1]);
        Assert.AreEqual(40, manager.OrdinaryItems[1].ProgressPercentage, 0.001);
    }

    [TestMethod]
    public void ApplyPageSnapshot_PutsSubscriptionItemsInTheirFolder()
    {
        using var manager = new DownloadManager(null, 1);
        var ordinary = Item("ordinary", "ordinary", DownloadState.Queued);
        var subscriptionItem = Item("subscription", "subscription", DownloadState.Completed, 100, 7);
        var folder = Folder(7, [subscriptionItem], "Subscription");

        manager.ApplyPageSnapshot(Page([ordinary], [folder]));

        Assert.HasCount(1, manager.OrdinaryItems);
        Assert.AreSame(ordinary, manager.OrdinaryItems[0]);
        Assert.HasCount(1, manager.Folders);
        Assert.AreEqual(7, manager.Folders[0].SubscriptionId);
        Assert.HasCount(1, manager.Folders[0].Items);
        Assert.AreSame(subscriptionItem, manager.Folders[0].Items[0]);
    }

    [TestMethod]
    public void Page_FiltersByStateAndSearchAndFollowsSnapshotChanges()
    {
        using var manager = new DownloadManager(null, 1);
        using var page = new DownloadPageViewModel(manager);
        var running = Item("running", "1", DownloadState.Running, 10, title: "Alpha");
        var completed = Item("completed", "2", DownloadState.Completed, 100, title: "Beta");
        manager.ApplyPageSnapshot(Page([running, completed], []));

        using var items = new DownloadItemPageViewModel(page);
        Assert.HasCount(2, items.View);

        items.CurrentOption = DownloadListOption.Running;
        Assert.HasCount(1, items.View);
        Assert.AreSame(running, items.View[0]);

        var progressed = Item("running", "1", DownloadState.Running, 40, title: "Alpha");
        manager.ApplyPageSnapshot(Page([progressed, completed], []));
        Assert.HasCount(1, items.View);
        Assert.AreSame(progressed, items.View[0]);
        Assert.AreEqual(40, items.View[0].ProgressPercentage, 0.001);

        items.CurrentOption = DownloadListOption.CustomSearch;
        items.FilterText = "beta";
        Assert.HasCount(1, items.View);
        Assert.AreEqual("2", items.View[0].ArtworkId);

        items.FilterText = "   ";
        Assert.HasCount(2, items.View);
    }

    [TestMethod]
    public void FolderPage_ShowsOnlyThatSubscription()
    {
        using var manager = new DownloadManager(null, 1);
        using var page = new DownloadPageViewModel(manager);
        var first = Item("first", "1", DownloadState.Completed, 100, 4, "First");
        var second = Item("second", "2", DownloadState.Queued, subscriptionId: 9, title: "Second");
        manager.ApplyPageSnapshot(Page([], [Folder(4, [first], "First"), Folder(9, [second], "Second")]));

        using var items = new DownloadItemPageViewModel(page, 4);
        Assert.HasCount(1, items.View);
        Assert.AreSame(first, items.View[0]);

        var replacement = Item("first", "1", DownloadState.Error, 100, 4, "First");
        manager.ApplyPageSnapshot(Page([], [Folder(4, [replacement], "First"), Folder(9, [second], "Second")]));
        Assert.HasCount(1, items.View);
        Assert.AreSame(replacement, items.View[0]);
        Assert.AreEqual(DownloadState.Error, items.View[0].State);
    }

    [TestMethod]
    public void SetSubscriptions_PublishesFoldersAndFetchFlags()
    {
        using var storage = new StorageEngine(":memory:");
        var unnamed = storage.UpsertSubscription(
            8,
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            "",
            "author",
            "avatar",
            "",
            null);
        var named = storage.UpsertSubscription(
            9,
            (uint)WorkSubscriptionType.Bookmarks,
            (uint)WorkSubscriptionWorkKind.Novel,
            "Named",
            "author",
            "named-avatar",
            "",
            null);
        var metas = WorkSubscriptionDownloadService.CreateFolderMetas(storage);
        Assert.HasCount(2, metas);
        Assert.AreEqual(named.HistoryEntryId, metas[0].SubscriptionId);
        Assert.AreEqual("Named", metas[0].DisplayName);
        Assert.AreEqual("named-avatar", metas[0].AvatarUrl);
        Assert.AreEqual((uint)WorkSubscriptionType.Bookmarks, metas[0].SubscriptionType);
        Assert.AreEqual((uint)WorkSubscriptionWorkKind.Novel, metas[0].WorkKind);
        Assert.AreEqual(unnamed.HistoryEntryId, metas[1].SubscriptionId);
        Assert.AreEqual("8", metas[1].DisplayName);

        using var manager = new DownloadManager(null, 1);
        manager.SetSubscriptions(metas);
        manager.SetFolderFetch(named.HistoryEntryId, true, 42u, null);
        manager.ApplyPageSnapshot(manager.CurrentPageSnapshot());

        Assert.HasCount(2, manager.Folders);
        Assert.AreEqual(named.HistoryEntryId, manager.Folders[0].SubscriptionId);
        Assert.IsTrue(manager.Folders[0].IsFetching);
        Assert.AreEqual(42u, manager.Folders[0].FetchedCount);
        Assert.AreEqual(0u, manager.Folders[0].TotalCount);
        Assert.AreEqual(DownloadState.Completed, manager.Folders[0].CurrentState);
        Assert.IsFalse(manager.Folders[1].IsFetching);

        manager.SetFolderFetch(named.HistoryEntryId, false, 0u, null);
        manager.ApplyPageSnapshot(manager.CurrentPageSnapshot());
        Assert.IsFalse(manager.Folders[0].IsFetching);
        Assert.AreEqual(0u, manager.Folders[0].FetchedCount);
    }

    [TestMethod]
    public void EnqueuePixiv_PlacesSubscriptionWorkInItsFolder()
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
        using var manager = new DownloadManager(null, 1);
        manager.BindStorage(storage);
        manager.SetSubscriptions(WorkSubscriptionDownloadService.CreateFolderMetas(storage));

        var directory = Path.Combine(Path.GetTempPath(), "pixeval-snapshot-" + Guid.NewGuid().ToString("N"));
        var macro = Path.Combine(directory, "@{id}.@{ext}");
        try
        {
            var ordinary = manager.EnqueuePixiv(IllustrationJson(11, "Ordinary"), false, -1, macro, "", 0, "");
            var subscribed = manager.EnqueuePixiv(
                IllustrationJson(22, "Subscribed"),
                false,
                -1,
                macro,
                "",
                subscription.HistoryEntryId,
                "Posts");
            Assert.IsFalse(string.IsNullOrEmpty(ordinary));
            Assert.IsFalse(string.IsNullOrEmpty(subscribed));

            manager.ApplyPageSnapshot(manager.CurrentPageSnapshot());
            Assert.HasCount(1, manager.OrdinaryItems);
            Assert.AreEqual("11", manager.OrdinaryItems[0].ArtworkId);
            Assert.AreEqual("Ordinary", manager.OrdinaryItems[0].Title);
            Assert.HasCount(1, manager.Folders);
            Assert.HasCount(1, manager.Folders[0].Items);
            Assert.AreEqual("22", manager.Folders[0].Items[0].ArtworkId);
            Assert.AreEqual(subscription.HistoryEntryId, manager.Folders[0].Items[0].WorkSubscriptionId);

            manager.CancelWork(manager.OrdinaryItems[0].Key);
            manager.CancelWork(manager.Folders[0].Items[0].Key);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static DownloadPageSnapshot Page(
        List<DownloadItemSnapshot> ordinary,
        List<DownloadFolderSnapshot> folders) =>
        new(ordinary, folders);

    private static DownloadItemSnapshot Item(
        string destination,
        string artworkId,
        DownloadState state,
        double progress = 0,
        long subscriptionId = 0,
        string title = "title") =>
        new(
            new DownloadTaskKey(destination, (int)subscriptionId, artworkId),
            title,
            "author",
            "",
            "",
            "",
            state,
            progress,
            1u,
            state == DownloadState.Completed ? 1u : 0u,
            state == DownloadState.Error ? 1u : 0u,
            null,
            destination,
            false,
            artworkId,
            subscriptionId);

    private static DownloadFolderSnapshot Folder(
        long subscriptionId,
        List<DownloadItemSnapshot> items,
        string displayName) =>
        new(
            subscriptionId,
            displayName,
            "",
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            items,
            (uint)items.Count,
            0u,
            0u,
            0u,
            100,
            DownloadState.Completed,
            false,
            0u,
            null);

    private static string IllustrationJson(long id, string title) =>
        $$"""
        {
          "Id": {{id}},
          "Title": "{{title}}",
          "IllustType": "illust",
          "ImageUrls": { "Large": "https://127.0.0.1:1/{{id}}.jpg" },
          "User": { "Id": 9, "Name": "Author", "Account": "author" },
          "CreateDate": "2020-01-02T03:04:05+00:00",
          "PageCount": 1,
          "MetaSinglePage": { "OriginalImageUrl": "https://127.0.0.1:1/{{id}}.jpg" }
        }
        """;
}
