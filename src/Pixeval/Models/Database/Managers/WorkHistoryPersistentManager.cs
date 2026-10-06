// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Diagnostics.CodeAnalysis;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.Models.Database.Managers;

public abstract class WorkHistoryPersistentManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TEntry>
    : ArtworkHistoryPersistentManager<TEntry>
    where TEntry : BrowseHistoryEntry, new()
{
    protected WorkHistoryPersistentManager(StorageEngine storage, FileLogger logger)
        : base(storage, logger)
    {
    }

    public abstract TEntry? GetByWorkKey(string workKey);

    public abstract void AddOrReplace(TEntry entry);

    public abstract bool TryDeleteByWorkKey(string workKey);
}
