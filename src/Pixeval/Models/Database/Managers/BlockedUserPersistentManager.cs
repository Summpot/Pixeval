// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Native.Storage;

namespace Pixeval.Models.Database.Managers;

public sealed class BlockedUserPersistentManager : SqlitePersistentManager
{
    private readonly HashSet<long> _blockedUserIds;

    public BlockedUserPersistentManager(StorageEngine storage) : base(storage)
    {
        _blockedUserIds = storage.GetAllBlockedUsers()
            .Select(static entry => entry.Id)
            .ToHashSet();
    }

    public int Count => (int)Storage.CountBlockedUsers();

    public FrozenSet<long> GetBlockedUserIds()
    {
        lock (_blockedUserIds)
            return _blockedUserIds.ToFrozenSet();
    }

    public BlockedUserRecord? GetByUserId(long userId)
    {
        if (userId <= 0)
            return null;

        return Storage.GetAllBlockedUsers().FirstOrDefault(u => u.Id == userId);
    }

    public void Insert(BlockedUserRecord entry) => Upsert(entry);

    public void AddOrUpdate(BlockedUserRecord entry) => Upsert(entry);

    public BlockedUserRecord Upsert(BlockedUserRecord entry)
    {
        var r = Storage.AddOrUpdateBlockedUser(entry.Id, entry.UserName, entry.AvatarUrl, entry.Account);
        lock (_blockedUserIds)
            _blockedUserIds.Add(entry.Id);
        return r;
    }

    public bool TryDeleteByUserId(long userId)
    {
        if (userId <= 0)
            return false;

        var ok = Storage.TryDeleteBlockedUser(userId);
        if (ok)
        {
            lock (_blockedUserIds)
                _blockedUserIds.Remove(userId);
        }
        return ok;
    }

    public bool TryDelete(BlockedUserRecord item) => TryDeleteByUserId(item.Id);

    public void Clear()
    {
        Storage.ClearBlockedUsers();
        lock (_blockedUserIds)
            _blockedUserIds.Clear();
    }

    public async IAsyncEnumerable<BlockedUserRecord> StreamEntriesAsync(
        int skip = 0,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var records = Storage.GetAllBlockedUsers();
        foreach (var r in records.Skip(skip))
        {
            token.ThrowIfCancellationRequested();
            yield return r;
        }
    }
}
