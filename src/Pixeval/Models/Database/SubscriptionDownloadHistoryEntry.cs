// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Misaki;

namespace Pixeval.Models.Database;

public sealed class SubscriptionDownloadHistoryEntry : DownloadHistoryEntryBase
{
    public SubscriptionDownloadHistoryEntry(
        string destination,
        IArtworkInfo entry,
        int workSubscriptionId) : base(destination, entry)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workSubscriptionId);
        if (string.IsNullOrWhiteSpace(entry.Id))
            throw new ArgumentException("The artwork ID cannot be empty.", nameof(entry));

        WorkSubscriptionId = workSubscriptionId;
        ArtworkId = entry.Id;
    }

    public SubscriptionDownloadHistoryEntry()
    {
    }

    public int WorkSubscriptionId { get; init; }

    public string ArtworkId { get; init; } = null!;

    /// <inheritdoc />
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public override string Destination { get; init; } = null!;
}
