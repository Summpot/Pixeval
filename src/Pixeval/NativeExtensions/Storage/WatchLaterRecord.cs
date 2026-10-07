// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Misaki;

namespace Pixeval.Native.Storage;

public partial record WatchLaterRecord
{
    private IArtworkInfo? _entry;

    internal IArtworkInfo? EntryOverride
    {
        get => _entry;
        init => _entry = value;
    }

    public IArtworkInfo? Entry => _entry ??= ArtworkPayloadHydrator.Hydrate(SerializeKey, PayloadJson);

    public static bool TryCreateWorkKey(IArtworkInfo entry, out string key)
    {
        key = "";
        if (entry is not ISerializable { SerializeKey: { } serializeKey }
            || string.IsNullOrEmpty(entry.Id))
            return false;

        key = CreateWorkKey(serializeKey, entry.Id);
        return true;
    }

    public static string CreateWorkKey(string serializeKey, string id) => $"{serializeKey}:{id}";
}
