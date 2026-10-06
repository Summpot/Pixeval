// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Models.Database;

public sealed class ArtworkPayloadEntry
{
    public ArtworkPayloadEntry()
    {
    }

    public ArtworkPayloadEntry(string serializedArtwork) => SerializedArtwork = serializedArtwork;

    public int ArtworkPayloadEntryId { get; set; }

    public string SerializedArtwork { get; init; } = null!;
}
