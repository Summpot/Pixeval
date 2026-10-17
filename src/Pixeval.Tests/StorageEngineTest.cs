// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Storage;

namespace Pixeval.Tests;

[TestClass]
public sealed class StorageEngineTest
{
    [TestMethod]
    public void SearchHistoryLifecycleShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");
        var history = storage.HistoryRepository;

        Assert.AreEqual(0, history.CountSearchHistory());

        var inserted = history.InsertSearchHistory("genshin", "原神", "2026-10-06T00:00:00Z");
        Assert.AreEqual("genshin", inserted.Value);
        Assert.AreEqual("原神", inserted.TranslatedName);
        Assert.AreEqual(1, history.CountSearchHistory());

        var found = history.GetSearchHistoryByValue("genshin");
        Assert.IsNotNull(found);
        Assert.AreEqual("原神", found.Value.TranslatedName);

        var upserted = history.UpsertSearchHistory("genshin", "原神冲击", "2026-10-06T01:00:00Z");
        Assert.AreEqual("原神冲击", upserted.TranslatedName);
        Assert.AreEqual(1, history.CountSearchHistory());

        var stream = history.StreamSearchHistories(0, 10);
        Assert.AreEqual(1, stream.Count);
        Assert.AreEqual("genshin", stream[0].Value);

        Assert.IsTrue(history.TryDeleteSearchHistoryByValue("genshin"));
        Assert.IsFalse(history.TryDeleteSearchHistoryByValue("genshin"));
        Assert.AreEqual(0, history.CountSearchHistory());
    }

    [TestMethod]
    public void BrowseHistoryCascadeAndWorkKeyShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");
        var history = storage.HistoryRepository;

        var rec = history.AddOrReplaceBrowseHistory(
            "12345",
            "Illustration",
            "Illustration:12345",
            "{\"title\":\"Test Artwork\"}");

        Assert.AreEqual("Illustration:12345", rec.WorkKey);
        Assert.AreEqual(1, history.CountBrowseHistory());

        var found = history.GetBrowseHistoryByWorkKey("Illustration:12345");
        Assert.IsNotNull(found);
        Assert.AreEqual("{\"title\":\"Test Artwork\"}", found.Value.PayloadJson);

        // Replacing should update payload and preserve single entry count
        var rec2 = history.AddOrReplaceBrowseHistory(
            "12345",
            "Illustration",
            "Illustration:12345",
            "{\"title\":\"Updated Artwork\"}");

        Assert.AreEqual(1, history.CountBrowseHistory());

        var stream = history.StreamBrowseHistory(0, 10);
        Assert.AreEqual(1, stream.Count);
        Assert.AreEqual("{\"title\":\"Updated Artwork\"}", stream[0].PayloadJson);

        history.ClearBrowseHistory();
        Assert.AreEqual(0, history.CountBrowseHistory());
        Assert.IsNull(history.GetBrowseHistoryByWorkKey("Illustration:12345"));
    }

    [TestMethod]
    public void WatchLaterAddAndRemoveShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");
        var watchLater = storage.WatchLaterRepository;

        Assert.IsFalse(watchLater.ContainsWatchLater("Illustration:999"));

        watchLater.AddOrReplaceWatchLater(
            "999",
            "Illustration",
            "Illustration:999",
            "{\"title\":\"Later Artwork\"}");

        Assert.IsTrue(watchLater.ContainsWatchLater("Illustration:999"));
        Assert.AreEqual(1, watchLater.CountWatchLater());

        var fetched = watchLater.GetWatchLaterByWorkKey("Illustration:999");
        Assert.IsNotNull(fetched);
        Assert.AreEqual("{\"title\":\"Later Artwork\"}", fetched.Value.PayloadJson);

        var list = watchLater.StreamWatchLater(0, 5);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("Illustration:999", list[0].WorkKey);

        Assert.IsTrue(watchLater.RemoveWatchLater("Illustration:999"));
        Assert.IsFalse(watchLater.ContainsWatchLater("Illustration:999"));
        Assert.AreEqual(0, watchLater.CountWatchLater());
        Assert.IsNull(watchLater.GetWatchLaterByWorkKey("Illustration:999"));
    }

    [TestMethod]
    public void DownloadHistoryAddDeleteAndStreamShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");
        var download = storage.DownloadRepository;

        Assert.AreEqual(0, download.CountDownloadHistory());

        download.AddOrReplaceDownloadHistory(
            "1001",
            "Illustration",
            "C:/downloads/1001.png",
            3,
            null,
            null,
            "{\"title\":\"Downloaded 1001\"}");

        Assert.AreEqual(1, download.CountDownloadHistory());

        var stream = download.StreamDownloadHistory(0, 10);
        Assert.AreEqual(1, stream.Count);
        Assert.AreEqual("C:/downloads/1001.png", stream[0].Destination);
        Assert.AreEqual(3u, stream[0].State);

        Assert.IsTrue(download.TryDeleteDownloadHistoryByDestination("C:/downloads/1001.png"));
        Assert.IsFalse(download.TryDeleteDownloadHistoryByDestination("C:/downloads/1001.png"));
        Assert.AreEqual(0, download.CountDownloadHistory());
    }

    [TestMethod]
    public void SubscriptionDownloadHistoryBatchAndOrphansShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");

        var items = new List<SubscriptionDownloadHistoryRecord>
        {
            new(
                0,
                "art1",
                "Illustration",
                "C:/sub/art1.jpg",
                3,
                null,
                null,
                10,
                "art1",
                "{\"title\":\"Sub Art 1\"}",
                null),
            new(
                0,
                "art2",
                "Illustration",
                "C:/sub/art2.jpg",
                3,
                null,
                null,
                20,
                "art2",
                "{\"title\":\"Sub Art 2\"}",
                null),
            new(
                0,
                "art3",
                "Illustration",
                "C:/sub/art3.jpg",
                3,
                null,
                null,
                30,
                "art3",
                "{\"title\":\"Sub Art 3\"}",
                null)
        };

        var download = storage.DownloadRepository;
        download.AddOrReplaceSubscriptionDownloadHistoryBatch(items);
        Assert.AreEqual(3, download.CountSubscriptionDownloadHistory());

        Assert.IsTrue(download.ContainsSubscriptionDownloadIdentity(10, "art1", "C:/sub/art1.jpg"));
        Assert.IsFalse(download.ContainsSubscriptionDownloadIdentity(10, "art1", "C:/other.jpg"));

        // Delete subscription 10 downloads
        var deletedSub10 = download.DeleteSubscriptionDownloadsByWorkSubscriptionId(10);
        Assert.AreEqual(1, deletedSub10);
        Assert.AreEqual(2, download.CountSubscriptionDownloadHistory());

        // Delete orphans: only sub 20 is valid now
        var deletedOrphans = download.DeleteOrphanSubscriptionDownloads(new List<long> { 20 });
        Assert.AreEqual(1, deletedOrphans); // sub 30 was orphan
        Assert.AreEqual(1, download.CountSubscriptionDownloadHistory());
    }

    [TestMethod]
    public void WorkSubscriptionUpsertAndQueryShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");

        var created = storage.UpsertSubscription(
            123456,
            1,
            0,
            "Artist Alpha",
            "Alpha",
            "avatar.png");

        Assert.IsTrue(created.HistoryEntryId > 0);
        Assert.AreEqual(123456, created.Id);
        Assert.AreEqual(1, storage.CountSubscriptions());

        var fetched = storage.GetSubscriptionByKey(123456, 1, 0);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("Artist Alpha", fetched.Value.Title);
        Assert.AreEqual("Alpha", fetched.Value.Author);
        Assert.AreEqual("avatar.png", fetched.Value.Avatar);

        var updated = storage.UpsertSubscription(
            123456,
            1,
            0,
            "Artist Alpha (Updated)",
            "Alpha",
            "avatar2.png");

        Assert.AreEqual(created.HistoryEntryId, updated.HistoryEntryId);
        Assert.AreEqual("Artist Alpha (Updated)", updated.Title);

        var ids = storage.GetAllSubscriptionHistoryIds();
        Assert.AreEqual(1, ids.Count);
        Assert.AreEqual(created.HistoryEntryId, ids[0]);

        Assert.IsTrue(storage.DeleteSubscription(created.HistoryEntryId));
        Assert.AreEqual(0, storage.CountSubscriptions());
    }

    [TestMethod]
    public void BlockedUsersLifecycleShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");

        Assert.IsFalse(storage.IsUserBlocked(777));

        var record = storage.AddOrUpdateBlockedUser(777, "BlockedSpammer", "avatar.png", "spammer_handle");
        Assert.AreEqual(777, record.Id);
        Assert.AreEqual("BlockedSpammer", record.UserName);
        Assert.AreEqual("avatar.png", record.AvatarUrl);
        Assert.AreEqual("spammer_handle", record.Account);
        Assert.IsTrue(storage.IsUserBlocked(777));
        Assert.AreEqual(1, storage.CountBlockedUsers());

        var all = storage.GetAllBlockedUsers();
        Assert.AreEqual(1, all.Count);
        Assert.AreEqual(777, all[0].Id);
        Assert.AreEqual("spammer_handle", all[0].Account);

        Assert.IsTrue(storage.TryDeleteBlockedUser(777));
        Assert.IsFalse(storage.IsUserBlocked(777));
        Assert.AreEqual(0, storage.CountBlockedUsers());
    }

    [TestMethod]
    public void LoginUserUpsertAndFetchShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");

        var user = new LoginUserRecord(
            0,
            88888,
            "PixevalUser",
            "user_account",
            "user@pixeval.org",
            true,
            0,
            true,
            true,
            "https://avatar16.png",
            "https://avatar50.png",
            "https://avatar170.png",
            "token_xyz_123");

        var upserted = storage.UpsertLoginUser(user);
        Assert.IsTrue(upserted.HistoryEntryId > 0);

        var fetched = storage.GetLoginUserByKey(upserted.HistoryEntryId);
        Assert.IsNotNull(fetched);
        Assert.AreEqual(88888, fetched.Value.UserId);
        Assert.AreEqual("PixevalUser", fetched.Value.Name);
        Assert.AreEqual("token_xyz_123", fetched.Value.RefreshToken);
        Assert.IsTrue(fetched.Value.IsPremium);

        var all = storage.GetAllLoginUsers();
        Assert.AreEqual(1, all.Count);

        Assert.IsTrue(storage.DeleteLoginUser(upserted.HistoryEntryId));
        Assert.IsNull(storage.GetLoginUserByKey(upserted.HistoryEntryId));
    }
}
