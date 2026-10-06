// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Imouto.BooruParser;
using Pixeval.Models.Pixiv;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misaki;
using Pixeval.Models.Database;
using Pixeval.Models.Database.Managers;
using Pixeval.Native.Storage;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Tests;

[TestClass]
public sealed class ArtworkHistoryPersistentManagerTest
{
    [TestMethod]
    public void Clear_RemovesEntries()
    {
        using var storage = new StorageEngine(":memory:");
        var logger = CreateLogger();
        var manager = new BrowseHistoryPersistentManager(storage, logger);
        for (var i = 0; i < 45; i++)
            manager.AddOrReplace(new(CreatePost(i.ToString())));

        manager.Clear();

        Assert.AreEqual(0, manager.Count);
    }

    [TestMethod]
    public async Task SearchHistoryPersistentManager_StreamEntriesAsyncPagesNewestFirst()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new SearchHistoryPersistentManager(storage);
        for (var i = 0; i < 45; i++)
            manager.Insert(new(i.ToString()));

        var values = new List<string>();
        await foreach (var entry in manager.StreamEntriesAsync(7))
            values.Add(entry.Value);

        Assert.HasCount(38, values);
        for (var i = 0; i < values.Count; i++)
            Assert.AreEqual((37 - i).ToString(), values[i]);
    }

    [TestMethod]
    public void SearchHistory_UpsertReplacesValueAndReturnsPersistedEntry()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new SearchHistoryPersistentManager(storage);
        var first = manager.Upsert(new("query", "old"));
        var entry = new SearchHistoryRecord("query", "new");

        var result = manager.Upsert(entry);

        Assert.AreEqual(entry.Value, result.Value);
        Assert.AreEqual("new", result.TranslatedName);
        Assert.AreNotEqual(0, result.HistoryEntryId);
        Assert.AreEqual(1, manager.Count);
        Assert.AreEqual("new", manager.GetByValue("query")?.TranslatedName);
    }

    [TestMethod]
    public async Task StreamAsync_LoadsPayloadAndKeepsOneToOneRelationship()
    {
        using var storage = new StorageEngine(":memory:");
        var logger = CreateLogger();
        var manager = new BrowseHistoryPersistentManager(storage, logger);
        var post = CreatePost("1");
        var changedCount = 0;
        manager.Changed += (_, _) => changedCount++;

        manager.AddOrReplace(new(post));
        manager.AddOrReplace(new(post));

        Assert.AreEqual(2, changedCount);
        Assert.AreEqual(1, manager.Count);
        await using var enumerator = manager.StreamAsync(SimpleWorkType.Illustration).GetAsyncEnumerator();
        Assert.IsTrue(await enumerator.MoveNextAsync());
        Assert.AreEqual(post.Id.Id, enumerator.Current.Id);
    }

    [TestMethod]
    public async Task SearchHistory_AddOrUpdateKeepsOnlyNewestEntry()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new SearchHistoryPersistentManager(storage);
        manager.AddOrUpdate(new("same", "old"));
        manager.AddOrUpdate(new("same", "new"));

        await using var enumerator = manager.StreamEntriesAsync().GetAsyncEnumerator();

        Assert.IsTrue(await enumerator.MoveNextAsync());
        Assert.AreEqual("new", enumerator.Current.TranslatedName);
        Assert.IsFalse(await enumerator.MoveNextAsync());
        Assert.AreEqual(1, manager.Count);
    }

    [TestMethod]
    public async Task StreamEntriesAsync_AppliesInitialSkipAndContinuesAcrossPages()
    {
        using var storage = new StorageEngine(":memory:");
        var logger = CreateLogger();
        var manager = new DownloadHistoryPersistentManager(storage, logger);
        for (var i = 0; i < 45; i++)
            manager.Insert(new(i.ToString(), CreatePost(i.ToString())));

        var destinations = new List<string>();
        await foreach (var entry in manager.StreamEntriesAsync(7))
            destinations.Add(entry.Destination);

        Assert.HasCount(38, destinations);
        for (var i = 0; i < destinations.Count; i++)
            Assert.AreEqual((37 - i).ToString(), destinations[i]);
    }

    [TestMethod]
    public void SubscriptionHistory_ContainsIdentityDoesNotLoadPayload()
    {
        using var storage = new StorageEngine(":memory:");
        var logger = CreateLogger();
        var manager = new SubscriptionDownloadHistoryPersistentManager(storage, logger);
        for (var i = 0; i < 45; i++)
            manager.Insert(new(i.ToString(), CreatePost(i.ToString()), i % 2 + 1));

        Assert.IsTrue(manager.ContainsIdentity(2, "43", "43"));
        Assert.IsFalse(manager.ContainsIdentity(1, "43", "43"));
    }

    [TestMethod]
    public async Task AddOrReplace_ReplacesDestinationAndPayload()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new DownloadHistoryPersistentManager(storage, CreateLogger());
        manager.Insert(new("same", CreatePost("old")));
        manager.AddOrReplace(new("same", CreatePost("new")));

        Assert.AreEqual(1, manager.Count);
        await using var enumerator = manager.StreamEntriesAsync().GetAsyncEnumerator();
        Assert.IsTrue(await enumerator.MoveNextAsync());
        Assert.AreEqual("new", enumerator.Current.Entry.Id);
        Assert.IsFalse(await enumerator.MoveNextAsync());
    }

    [TestMethod]
    public async Task SubscriptionHistory_AddOrReplaceUsesCompositeIdentity()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new SubscriptionDownloadHistoryPersistentManager(storage, CreateLogger());
        manager.AddOrReplace(new("same", CreatePost("artwork"), 1));

        manager.AddOrReplace(new("same", CreatePost("artwork"), 1));
        manager.AddOrReplace(new("same", CreatePost("artwork"), 2));
        manager.AddOrReplace(new("same", CreatePost("other"), 1));

        Assert.AreEqual(3, manager.Count);
        var identities = new List<(int SubscriptionId, string ArtworkId, string Destination)>();
        await foreach (var entry in manager.StreamEntriesAsync())
            identities.Add((entry.WorkSubscriptionId, entry.ArtworkId, entry.Destination));
        Assert.AreSequenceEqual(
            [(1, "artwork", "same"), (2, "artwork", "same"), (1, "other", "same")], identities, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public async Task SubscriptionHistory_AddOrReplaceRangeCommitsAsSingleBatch()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new SubscriptionDownloadHistoryPersistentManager(storage, CreateLogger());
        manager.AddOrReplace(new("same", CreatePost("old"), 1));
        var changedCount = 0;
        manager.Changed += (_, _) => changedCount++;

        manager.AddOrReplaceRange(
        [
            new("same", CreatePost("old"), 1),
            new("second", CreatePost("second"), 1),
            new("third", CreatePost("third"), 2)
        ]);

        Assert.AreEqual(1, changedCount);
        Assert.AreEqual(3, manager.Count);
        var identities = new List<(int SubscriptionId, string ArtworkId, string Destination)>();
        await foreach (var entry in manager.StreamEntriesAsync())
            identities.Add((entry.WorkSubscriptionId, entry.ArtworkId, entry.Destination));
        Assert.AreSequenceEqual(
            [(1, "old", "same"), (1, "second", "second"), (2, "third", "third")], identities, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void SubscriptionHistory_DeleteBySubscriptionRemovesOwnedPayloads()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new SubscriptionDownloadHistoryPersistentManager(storage, CreateLogger());
        manager.AddOrReplace(new("first", CreatePost("first"), 1));
        manager.AddOrReplace(new("second", CreatePost("second"), 1));
        manager.AddOrReplace(new("other", CreatePost("other"), 2));

        var deletedCount = manager.DeleteByWorkSubscriptionId(1);

        Assert.AreEqual(2, deletedCount);
        Assert.AreEqual(1, manager.Count);
        Assert.IsTrue(manager.ContainsIdentity(2, "other", "other"));
    }

    [TestMethod]
    public void SubscriptionHistory_DeleteOrphansPreservesKnownSubscriptions()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new SubscriptionDownloadHistoryPersistentManager(storage, CreateLogger());
        manager.AddOrReplace(new("first", CreatePost("first"), 1));
        manager.AddOrReplace(new("second", CreatePost("second"), 2));
        manager.AddOrReplace(new("third", CreatePost("third"), 3));

        var deletedCount = manager.DeleteOrphans(new HashSet<int> { 2 });

        Assert.AreEqual(2, deletedCount);
        Assert.AreEqual(1, manager.Count);
        Assert.IsTrue(manager.ContainsIdentity(2, "second", "second"));
    }

    [TestMethod]
    public void DownloadHistory_ClearOnlyRemovesOwnedPayloads()
    {
        using var storage = new StorageEngine(":memory:");
        var logger = CreateLogger();
        var ordinaryManager = new DownloadHistoryPersistentManager(storage, logger);
        var subscriptionManager = new SubscriptionDownloadHistoryPersistentManager(storage, logger);
        ordinaryManager.AddOrReplace(new("ordinary", CreatePost("ordinary")));
        subscriptionManager.AddOrReplace(new("subscription", CreatePost("subscription"), 1));

        ordinaryManager.Clear();

        Assert.AreEqual(0, ordinaryManager.Count);
        Assert.AreEqual(1, subscriptionManager.Count);

        subscriptionManager.Clear();

        Assert.AreEqual(0, subscriptionManager.Count);
    }

    [TestMethod]
    public async Task LoginUsers_LoadRestoresCurrentSelection()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new LoginUserPersistentManager(storage);
        var user = manager.Upsert(new LoginUserRecord(
            0,
            1,
            "user",
            "",
            "",
            false,
            0,
            false,
            false,
            "",
            "",
            "",
            "refresh-token"));
        var viewModel = new LoginPageViewModel(manager, (int)user.HistoryEntryId);

        await viewModel.LoadUsersAsync();

        Assert.AreEqual(user.HistoryEntryId, viewModel.SelectedUser?.HistoryEntryId);
        Assert.AreEqual(user.RefreshToken, viewModel.RefreshToken);
    }

    [TestMethod]
    public void LoginUsers_UpsertReturnsUpdatedEntry()
    {
        using var storage = new StorageEngine(":memory:");
        var manager = new LoginUserPersistentManager(storage);
        var existing = manager.Upsert(new LoginUserRecord(
            0,
            1,
            "old-name",
            "",
            "",
            false,
            0,
            false,
            false,
            "",
            "",
            "",
            "old-token"));

        var result = manager.Upsert(new LoginUserRecord(
            existing.HistoryEntryId,
            1,
            "new-name",
            "",
            "",
            false,
            0,
            false,
            false,
            "",
            "",
            "",
            "new-token"));

        Assert.AreEqual(existing.HistoryEntryId, result.HistoryEntryId);
        Assert.AreEqual("new-token", result.RefreshToken);
        Assert.AreEqual("new-name", result.Name);
    }

    private static FileLogger CreateLogger() => new(Path.Combine(
        Path.GetTempPath(),
        nameof(ArtworkHistoryPersistentManagerTest),
        Guid.NewGuid().ToString("N")));

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
