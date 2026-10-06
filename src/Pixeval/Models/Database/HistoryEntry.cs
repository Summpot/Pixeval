// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Models.Database;

public abstract class HistoryEntry : IEquatable<HistoryEntry>
{
    public int HistoryEntryId { get; set; }

    public bool Equals(HistoryEntry? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return HistoryEntryId == other.HistoryEntryId;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as HistoryEntry);
    }

    /// <inheritdoc />
    public override int GetHashCode() => HistoryEntryId;
}
