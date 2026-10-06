// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Models.Database.Managers;
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
        var subManager = new WorkSubscriptionPersistentManager(storage);
        var blockedManager = new BlockedUserPersistentManager(storage);

        subManager.AddOrUpdate(new WorkSubscriptionRecord(
            12345,
            WorkSubscriptionType.Posts,
            WorkSubscriptionWorkKind.Illustration,
            "Artist 1"));

        blockedManager.AddOrUpdate(new BlockedUserRecord(67890, "Blocked 1"));

        Assert.AreEqual(1, subManager.Count);
        Assert.AreEqual(1, blockedManager.Count);

        var sub = subManager.GetBySubscriptionKey(12345, WorkSubscriptionType.Posts, WorkSubscriptionWorkKind.Illustration);
        Assert.IsNotNull(sub);
        Assert.AreEqual("Artist 1", sub.Name);

        var blocked = blockedManager.GetByUserId(67890);
        Assert.IsNotNull(blocked);
        Assert.AreEqual("Blocked 1", blocked.Name);
    }
}
