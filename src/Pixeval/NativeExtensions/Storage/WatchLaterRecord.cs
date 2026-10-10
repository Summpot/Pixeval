// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Native.Storage;

public partial record WatchLaterRecord
{
    private object? _entry;

    internal object? EntryOverride
    {
        get => _entry;
        init => _entry = value;
    }

    public object? Entry => _entry ??= ArtworkPayloadHydrator.Hydrate(SerializeKey, PayloadJson);

    public static bool TryCreateWorkKey(object entry, out string key)
    {
        key = "";
        if (entry is not IArtworkSerializable serializable)
            return false;

        var id = entry switch
        {
            Mako.Illustration i => i.Id.ToString(),
            Mako.Novel n => n.Id.ToString(),
            Booru.BooruPost b => b.Id,
            SauceNao.SauceNaoItem s => s.RawId,
            _ => null
        };

        if (string.IsNullOrEmpty(id))
            return false;

        key = CreateWorkKey(serializable.SerializeKey, id);
        return true;
    }

    public static string CreateWorkKey(string serializeKey, string id) => $"{serializeKey}:{id}";
}
