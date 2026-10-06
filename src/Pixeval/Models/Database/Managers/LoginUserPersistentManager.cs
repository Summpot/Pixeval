// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Native.Storage;

namespace Pixeval.Models.Database.Managers;

public class LoginUserPersistentManager : SqlitePersistentManager
{
    public LoginUserPersistentManager(StorageEngine storage) : base(storage)
    {
    }

    public int Count => Storage.GetAllLoginUsers().Count;

    public LoginUserRecord? GetByKey(int key)
    {
        if (key <= 0)
            return null;

        return Storage.GetLoginUserByKey(key);
    }

    public LoginUserRecord? GetByRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var all = Storage.GetAllLoginUsers();
        return all.FirstOrDefault(u => u.RefreshToken == refreshToken);
    }

    public LoginUserRecord? GetByUserId(long userId)
    {
        if (userId <= 0)
            return null;

        var all = Storage.GetAllLoginUsers();
        return all.FirstOrDefault(u => u.UserId == userId);
    }

    public void AddOrUpdate(LoginUserRecord entry) => Upsert(entry);

    public LoginUserRecord Upsert(LoginUserRecord entry)
    {
        return Storage.UpsertLoginUser(entry);
    }

    public bool TryDelete(LoginUserRecord item)
    {
        return Storage.DeleteLoginUser(item.HistoryEntryId);
    }

    public void Clear()
    {
        Storage.ClearLoginUsers();
    }

    public async IAsyncEnumerable<LoginUserRecord> StreamEntriesAsync(
        int skip = 0,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var users = Storage.GetAllLoginUsers().Skip(skip);
        foreach (var u in users)
        {
            token.ThrowIfCancellationRequested();
            yield return u;
        }
    }
}
