// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Native.Storage;

namespace Pixeval.Models.Database.Managers;

public abstract class SqlitePersistentManager
{
    protected StorageEngine Storage { get; }

    protected SqlitePersistentManager(StorageEngine storage)
    {
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }
}
