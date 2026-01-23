// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System.Diagnostics.CodeAnalysis;
using SQLite;

namespace Pixeval.Models.Database;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public abstract class HistoryEntry
{
    [PrimaryKey, AutoIncrement]
    public int HistoryEntryId { get; set; }
}
