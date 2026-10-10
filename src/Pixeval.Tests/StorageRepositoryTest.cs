// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Booru;
using Pixeval.Native.Storage;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Tests;

[TestClass]
public sealed class StorageRepositoryTest
{
    [TestMethod]
    public void BrowseHistory_AddAndClear_WorksCorrectly()
    {
        using var storage = new StorageEngine(":memory:");
        var repo = storage.HistoryRepository;

        for (var i = 0; i < 45; i++)
        {
            var post = CreatePost(i.ToString());
            repo.AddBrowseHistory(post);
        }

        Assert.AreEqual(45, repo.CountBrowseHistory());

        repo.ClearBrowseHistory();

        Assert.AreEqual(0, repo.CountBrowseHistory());
    }

    [TestMethod]
    public void SearchHistory_CursorPagination_PagesNewestFirst()
    {
        using var storage = new StorageEngine(":memory:");
        var repo = storage.HistoryRepository;

        for (var i = 0; i < 45; i++)
            repo.AddSearchHistory(i.ToString());

        var values = new List<string>();
        long? cursor = null;
        while (true)
        {
            var batch = repo.StreamSearchHistoriesCursor(cursor, 10);
            if (batch.Count == 0)
                break;
            values.AddRange(batch.Select(b => b.Value));
            cursor = batch[^1].HistoryEntryId;
        }

        Assert.AreEqual(45, values.Count);
        for (var i = 0; i < values.Count; i++)
            Assert.AreEqual((44 - i).ToString(), values[i]);
    }

    [TestMethod]
    public void SearchHistory_UpsertReplacesValueAndReturnsPersistedEntry()
    {
        using var storage = new StorageEngine(":memory:");
        var repo = storage.HistoryRepository;

        var first = repo.UpsertSearchHistory("query", "old", "2026-01-01T00:00:00Z");
        var result = repo.UpsertSearchHistory("query", "new", "2026-01-02T00:00:00Z");

        Assert.AreEqual("query", result.Value);
        Assert.AreEqual("new", result.TranslatedName);
        Assert.AreEqual(1, repo.CountSearchHistory());
        Assert.AreEqual("new", repo.GetSearchHistoryByValue("query")?.TranslatedName);
    }

    [TestMethod]
    public async Task BrowseHistory_StreamAsync_HydratesPayload()
    {
        using var storage = new StorageEngine(":memory:");
        var repo = storage.HistoryRepository;
        var post = CreatePost("1");

        repo.AddBrowseHistory(post);
        repo.AddBrowseHistory(post);

        Assert.AreEqual(1, repo.CountBrowseHistory());
        await using var enumerator = repo.StreamAsync(SimpleWorkType.Illustration).GetAsyncEnumerator();
        Assert.IsTrue(await enumerator.MoveNextAsync());
        Assert.AreEqual(post.Id, ((BooruPost) enumerator.Current).Id);
    }

    [TestMethod]
    public void WatchLater_AddAndContainsAndRemove_WorksCorrectly()
    {
        using var storage = new StorageEngine(":memory:");
        var repo = storage.WatchLaterRepository;
        var post = CreatePost("100");

        Assert.IsFalse(repo.ContainsWatchLater(post));
        Assert.IsTrue(repo.AddWatchLater(post));
        Assert.IsTrue(repo.ContainsWatchLater(post));
        Assert.AreEqual(1, repo.CountWatchLater());

        Assert.IsTrue(repo.RemoveWatchLater(post));
        Assert.IsFalse(repo.ContainsWatchLater(post));
        Assert.AreEqual(0, repo.CountWatchLater());
    }

    [TestMethod]
    public void DownloadHistory_AddOrReplaceAndStateUpdate()
    {
        using var storage = new StorageEngine(":memory:");
        var repo = storage.DownloadRepository;

        var record = repo.AddOrReplaceDownloadHistory(
            "1",
            "Mako.Model.Illustration",
            "dest/1.jpg",
            1, // Queued
            null,
            null,
            "{}");

        Assert.AreEqual("dest/1.jpg", record.Destination);
        Assert.AreEqual(1, repo.CountDownloadHistory());

        var updated = repo.UpdateDownloadHistoryState("dest/1.jpg", 3, null); // Completed
        Assert.IsTrue(updated);

        var retrieved = repo.GetDownloadHistoryByDestination("dest/1.jpg");
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(3u, retrieved.State);

        var deleted = repo.TryDeleteDownloadHistoryByDestination("dest/1.jpg");
        Assert.IsTrue(deleted);
        Assert.AreEqual(0, repo.CountDownloadHistory());
    }

    [TestMethod]
    public void SubscriptionDownloadHistory_BatchAndIdentity_WorksCorrectly()
    {
        using var storage = new StorageEngine(":memory:");
        var repo = storage.DownloadRepository;

        repo.AddOrReplaceSubscriptionDownloadHistoryBatch(
        [
            new(0, "1", null, "dest/1.jpg", 1, null, null, 10, "1", "{}"),
            new(0, "2", null, "dest/2.jpg", 1, null, null, 10, "2", "{}"),
            new(0, "3", null, "dest/3.jpg", 1, null, null, 20, "3", "{}"),
        ]);

        Assert.AreEqual(3, repo.CountSubscriptionDownloadHistory());
        Assert.IsTrue(repo.ContainsSubscriptionDownloadIdentity(10, "1", "dest/1.jpg"));
        Assert.IsFalse(repo.ContainsSubscriptionDownloadIdentity(10, "99", "dest/99.jpg"));

        var deletedBySub = repo.DeleteSubscriptionDownloadsByWorkSubscriptionId(10);
        Assert.AreEqual(2, deletedBySub);
        Assert.AreEqual(1, repo.CountSubscriptionDownloadHistory());

        var deletedOrphans = repo.DeleteOrphanSubscriptionDownloads([999]);
        Assert.AreEqual(1, deletedOrphans);
        Assert.AreEqual(0, repo.CountSubscriptionDownloadHistory());
    }

    [TestMethod]
    public async Task LoginUsers_LoadRestoresCurrentSelection()
    {
        using var storage = new StorageEngine(":memory:");
        var user = storage.UpsertLoginUser(new LoginUserRecord(
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

        var viewModel = new LoginPageViewModel(storage, (int)user.HistoryEntryId);
        await viewModel.LoadUsersAsync();

        Assert.AreEqual(user.HistoryEntryId, viewModel.SelectedUser?.HistoryEntryId);
        Assert.AreEqual(user.RefreshToken, viewModel.RefreshToken);
    }

    [TestMethod]
    public void LoginUsers_UpsertReturnsUpdatedEntry()
    {
        using var storage = new StorageEngine(":memory:");
        var existing = storage.UpsertLoginUser(new LoginUserRecord(
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

        var result = storage.UpsertLoginUser(new LoginUserRecord(
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

    [TestMethod]
    public void ArtworkPayloadHydrator_LegacyImoutoJson_HydratesCorrectly()
    {
        var legacyJson = """
        {
          "Id": {
            "Id": "12345",
            "Md5Hash": "abcdef123456",
            "PlatformType": 0
          },
          "OriginalUrl": "https://danbooru.donmai.us/data/sample.jpg",
          "SampleUrl": "https://danbooru.donmai.us/data/sample.jpg",
          "PreviewUrl": "https://danbooru.donmai.us/data/preview.jpg",
          "ExistState": 0,
          "CreateDate": "2026-01-01T00:00:00+00:00",
          "Uploader": {
            "Id": "1",
            "Name": "test_uploader",
            "Platform": 0
          },
          "Source": "https://twitter.com/test",
          "FileResolution": {
            "Width": 1920,
            "Height": 1080
          },
          "ByteSize": 102400,
          "SafeRating": 1,
          "Tags": [
            {
              "Type": "character",
              "Name": "hatsune_miku"
            }
          ]
        }
        """;

        var hydrated = ArtworkPayloadHydrator.Hydrate("Imouto.BooruParser.Post", legacyJson);
        Assert.IsNotNull(hydrated);
        Assert.IsInstanceOfType<BooruPost>(hydrated);
        var post = (BooruPost) hydrated;
        Assert.AreEqual("12345", post.Id);
        Assert.AreEqual(BooruPlatform.Danbooru, post.Platform);
        Assert.AreEqual(1920u, post.Width);
        Assert.AreEqual(1080u, post.Height);
        Assert.AreEqual("test_uploader", post.UploaderName);
        Assert.AreEqual(1, post.Tags.Count);
        Assert.AreEqual("hatsune_miku", post.Tags[0].Name);
        Assert.AreEqual("character", post.Tags[0].TagType);
    }

    private static BooruPost CreatePost(string id) => new(
        id,
        $"hash-{id}",
        BooruPlatform.Danbooru,
        $"https://example.com/{id}.jpg",
        null,
        null,
        100,
        100,
        0,
        "jpg",
        DateTimeOffset.UtcNow.ToString("O"),
        "1",
        "uploader",
        null,
        "general",
        [],
        null,
        false,
        0,
        false,
        false,
        null);
}
