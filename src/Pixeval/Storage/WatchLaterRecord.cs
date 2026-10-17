// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Storage;

public partial record struct WatchLaterRecord
{
    private object? _entry;

    internal object? EntryOverride
    {
        get => _entry;
        init => _entry = value;
    }

    public object? Entry => _entry ??= ArtworkPayloads.EntryOf(Payload);

    public static bool TryCreateWorkKey(object entry, out string key) =>
        ArtworkPayloads.TryCreateWorkKey(entry, out key);
}
