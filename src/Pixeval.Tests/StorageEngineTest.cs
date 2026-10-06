// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native.Storage;

namespace Pixeval.Tests;

[TestClass]
public sealed class StorageEngineTest
{
    [TestMethod]
    public void SearchHistoryLifecycleShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");

        Assert.AreEqual(0, storage.CountSearchHistory());

        var inserted = storage.InsertSearchHistory("genshin", "原神", "2026-10-06T00:00:00Z");
        Assert.AreEqual("genshin", inserted.Value);
        Assert.AreEqual("原神", inserted.TranslatedName);
        Assert.AreEqual(1, storage.CountSearchHistory());

        var found = storage.GetSearchHistoryByValue("genshin");
        Assert.IsNotNull(found);
        Assert.AreEqual("原神", found.TranslatedName);

        var upserted = storage.UpsertSearchHistory("genshin", "原神冲击", "2026-10-06T01:00:00Z");
        Assert.AreEqual("原神冲击", upserted.TranslatedName);
        Assert.AreEqual(1, storage.CountSearchHistory());

        var stream = storage.StreamSearchHistories(0, 10);
        Assert.AreEqual(1, stream.Count);
        Assert.AreEqual("genshin", stream[0].Value);

        Assert.IsTrue(storage.TryDeleteSearchHistoryByValue("genshin"));
        Assert.IsFalse(storage.TryDeleteSearchHistoryByValue("genshin"));
        Assert.AreEqual(0, storage.CountSearchHistory());
    }

    [TestMethod]
    public void BrowseHistoryCascadeAndWorkKeyShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");

        var rec = storage.AddOrReplaceBrowseHistory(
            "12345",
            "Illustration",
            "Illustration:12345",
            "{\"title\":\"Test Artwork\"}");

        Assert.AreEqual("Illustration:12345", rec.WorkKey);
        Assert.AreEqual(1, storage.CountBrowseHistory());

        var found = storage.GetBrowseHistoryByWorkKey("Illustration:12345");
        Assert.IsNotNull(found);
        Assert.AreEqual("{\"title\":\"Test Artwork\"}", found.PayloadJson);

        // Replacing should update payload and preserve single entry count
        var rec2 = storage.AddOrReplaceBrowseHistory(
            "12345",
            "Illustration",
            "Illustration:12345",
            "{\"title\":\"Updated Artwork\"}");

        Assert.AreEqual(1, storage.CountBrowseHistory());

        var stream = storage.StreamBrowseHistory(0, 10);
        Assert.AreEqual(1, stream.Count);
        Assert.AreEqual("{\"title\":\"Updated Artwork\"}", stream[0].PayloadJson);

        storage.ClearBrowseHistory();
        Assert.AreEqual(0, storage.CountBrowseHistory());
        Assert.IsNull(storage.GetBrowseHistoryByWorkKey("Illustration:12345"));
    }

    [TestMethod]
    public void WatchLaterAddAndRemoveShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");

        Assert.IsFalse(storage.ContainsWatchLater("Illustration:999"));

        storage.AddOrReplaceWatchLater(
            "999",
            "Illustration",
            "Illustration:999",
            "{\"title\":\"Later Artwork\"}");

        Assert.IsTrue(storage.ContainsWatchLater("Illustration:999"));
        Assert.AreEqual(1, storage.CountWatchLater());

        var fetched = storage.GetWatchLaterByWorkKey("Illustration:999");
        Assert.IsNotNull(fetched);
        Assert.AreEqual("{\"title\":\"Later Artwork\"}", fetched.PayloadJson);

        var list = storage.StreamWatchLater(0, 5);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("Illustration:999", list[0].WorkKey);

        Assert.IsTrue(storage.RemoveWatchLater("Illustration:999"));
        Assert.IsFalse(storage.ContainsWatchLater("Illustration:999"));
        Assert.AreEqual(0, storage.CountWatchLater());
        Assert.IsNull(storage.GetWatchLaterByWorkKey("Illustration:999"));
    }

    [TestMethod]
    public void DownloadHistoryAddDeleteAndStreamShouldSucceed()
    {
        using var storage = new StorageEngine(":memory:");

        Assert.AreEqual(0, storage.CountDownloadHistory());

        storage.AddOrReplaceDownloadHistory(
            "1001",
            "Illustration",
            "C:/downloads/1001.png",
            3,
            null,
            null,
            "{\"title\":\"Downloaded 1001\"}");

        Assert.AreEqual(1, storage.CountDownloadHistory());

        var stream = storage.StreamDownloadHistory(0, 10);
        Assert.AreEqual(1, stream.Count);
        Assert.AreEqual("C:/downloads/1001.png", stream[0].Destination);
        Assert.AreEqual(3u, stream[0].State);

        Assert.IsTrue(storage.TryDeleteDownloadHistoryByDestination("C:/downloads/1001.png"));
        Assert.IsFalse(storage.TryDeleteDownloadHistoryByDestination("C:/downloads/1001.png"));
        Assert.AreEqual(0, storage.CountDownloadHistory());
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
                "{\"title\":\"Sub Art 1\"}"),
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
                "{\"title\":\"Sub Art 2\"}"),
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
                "{\"title\":\"Sub Art 3\"}")
        };

        storage.AddOrReplaceSubscriptionDownloadHistoryBatch(items);
        Assert.AreEqual(3, storage.CountSubscriptionDownloadHistory());

        Assert.IsTrue(storage.ContainsSubscriptionDownloadIdentity(10, "art1", "C:/sub/art1.jpg"));
        Assert.IsFalse(storage.ContainsSubscriptionDownloadIdentity(10, "art1", "C:/other.jpg"));

        // Delete subscription 10 downloads
        var deletedSub10 = storage.DeleteSubscriptionDownloadsByWorkSubscriptionId(10);
        Assert.AreEqual(1, deletedSub10);
        Assert.AreEqual(2, storage.CountSubscriptionDownloadHistory());

        // Delete orphans: only sub 20 is valid now
        var deletedOrphans = storage.DeleteOrphanSubscriptionDownloads(new List<long> { 20 });
        Assert.AreEqual(1, deletedOrphans); // sub 30 was orphan
        Assert.AreEqual(1, storage.CountSubscriptionDownloadHistory());
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
            "avatar.png",
            "2026-10-06",
            "99999");

        Assert.IsTrue(created.HistoryEntryId > 0);
        Assert.AreEqual(123456, created.Id);
        Assert.AreEqual(1, storage.CountSubscriptions());

        var fetched = storage.GetSubscriptionByKey(123456, 1, 0);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("Artist Alpha", fetched.Title);
        Assert.AreEqual("Alpha", fetched.Author);
        Assert.AreEqual("avatar.png", fetched.Avatar);

        var updated = storage.UpsertSubscription(
            123456,
            1,
            0,
            "Artist Alpha (Updated)",
            "Alpha",
            "avatar2.png",
            "2026-10-06T03:00:00Z",
            "100000");

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
        Assert.AreEqual(88888, fetched.UserId);
        Assert.AreEqual("PixevalUser", fetched.Name);
        Assert.AreEqual("token_xyz_123", fetched.RefreshToken);
        Assert.IsTrue(fetched.IsPremium);

        var all = storage.GetAllLoginUsers();
        Assert.AreEqual(1, all.Count);

        Assert.IsTrue(storage.DeleteLoginUser(upserted.HistoryEntryId));
        Assert.IsNull(storage.GetLoginUserByKey(upserted.HistoryEntryId));
    }
}
