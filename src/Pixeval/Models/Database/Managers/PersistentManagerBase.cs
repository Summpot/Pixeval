// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using Pixeval.Native.Storage;

namespace Pixeval.Models.Database.Managers;

public abstract class PersistentManagerBase<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TEntry, TModel>
    : SqlitePersistentManager, IPersistentManager<TEntry, TModel>
    where TEntry : HistoryEntry, new()
{
    protected PersistentManagerBase(StorageEngine storage) : base(storage)
    {
    }

    /// <inheritdoc />
    public abstract int Count { get; }

    /// <inheritdoc />
    public virtual void Insert(TEntry entry) => Upsert(entry);

    /// <inheritdoc />
    public abstract IAsyncEnumerable<TModel> StreamEntriesAsync(int skip = 0, CancellationToken token = default);

    /// <inheritdoc />
    public abstract void AddOrUpdate(TEntry entry);

    /// <inheritdoc />
    public abstract TEntry Upsert(TEntry entry);

    /// <inheritdoc />
    public virtual void Update(TEntry entry) => AddOrUpdate(entry);

    /// <inheritdoc />
    public virtual bool TryDelete(TEntry item) => false;

    /// <inheritdoc />
    public virtual TEntry? TryDelete(Expression<Func<TEntry, bool>> predicate) =>
        throw new NotSupportedException("Expression deletion is deprecated with StorageEngine.");

    /// <inheritdoc />
    public virtual int Delete(Expression<Func<TEntry, bool>> predicate) =>
        throw new NotSupportedException("Expression deletion is deprecated with StorageEngine.");

    /// <inheritdoc />
    public abstract void Clear();

    protected abstract TModel ToModel(TEntry entry);
}
