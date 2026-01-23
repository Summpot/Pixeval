// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System;
using System.Diagnostics.CodeAnalysis;
using Misaki;
using SQLite;

namespace Pixeval.Models.Database;

public class BrowseHistoryEntry() : HistoryEntry
{
    public BrowseHistoryEntry(IArtworkInfo entry) : this()
    {
        if (entry is not ISerializable serializable)
        {
            throw new InvalidCastException($"{nameof(entry)} should be {nameof(ISerializable)}");
        }

        Entry = entry;
        Id = entry.Id;
        SerializeKey = serializable.SerializeKey;
        EntryString = serializable.Serialize();
    }

    [Ignore]
    [field: AllowNull, MaybeNull]
    public IArtworkInfo Entry => field ??= (IArtworkInfo) ArtworkSerializerTable.ArtworkTypeMethodsTable[SerializeKey](EntryString);

    // private set 反序列化使用
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    public string Id { get; private set; } = null!;

    // private set 反序列化使用
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    public string SerializeKey { get; private set; } = null!;

    // private set 反序列化使用
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    public string EntryString { get; private set; } = null!;
}
