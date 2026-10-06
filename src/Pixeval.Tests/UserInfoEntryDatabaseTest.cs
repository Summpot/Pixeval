// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Models.Options;
using Pixeval.Native.Storage;

namespace Pixeval.Tests;

[TestClass]
public sealed class UserInfoEntryDatabaseTest
{
    [TestMethod]
    public void DerivedEntries_CanPersistAndQueryIndependently()
    {
        using var storage = new StorageEngine(":memory:");

        storage.UpsertSubscription(
            12345,
            (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration,
            "Artist 1",
            "Artist 1",
            "",
            "",
            null);

        storage.AddOrUpdateBlockedUser(67890, "Blocked 1", "", "blocked_1");

        Assert.AreEqual(1, storage.CountSubscriptions());
        Assert.AreEqual(1, storage.CountBlockedUsers());

        var sub = storage.GetSubscriptionByKey(12345, (uint)WorkSubscriptionType.Posts, (uint)WorkSubscriptionWorkKind.Illustration);
        Assert.IsNotNull(sub);
        Assert.AreEqual("Artist 1", sub.Title);

        var blockedUsers = storage.GetAllBlockedUsers();
        Assert.AreEqual(1, blockedUsers.Count);
        Assert.AreEqual(67890, blockedUsers[0].Id);
        Assert.AreEqual("Blocked 1", blockedUsers[0].UserName);
    }
}
