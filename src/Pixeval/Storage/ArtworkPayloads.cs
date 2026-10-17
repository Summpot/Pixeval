// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Booru;
using Pixeval.Mako;
using Pixeval.SauceNao;

namespace Pixeval.Storage;

internal static class ArtworkPayloads
{
    public static object Unwrap(object entry) => entry switch
    {
        WorkEntry workEntry => workEntry.AsWorkEntry,
        WorkEntry.Illust illust => illust.Illustration,
        WorkEntry.NovelWork novel => novel.Novel,
        ArtworkPayload.Work work => Unwrap(work.Entry),
        ArtworkPayload.Booru booru => booru.Post,
        ArtworkPayload.SauceNao sauce => sauce.Item,
        _ => entry
    };

    public static bool TryGet(ArtworkPayload? payload, out ArtworkPayload value)
    {
        if (payload is object boxed && boxed is ArtworkPayload typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    public static bool TryWrap(object? entry, out ArtworkPayload payload)
    {
        payload = default!;
        if (entry is null)
            return false;
        if (entry is ArtworkPayload existing)
        {
            payload = existing;
            return true;
        }

        var inner = Unwrap(entry);
        switch (inner)
        {
            case Illustration illustration:
                payload = new ArtworkPayload(new ArtworkPayload.Work(new WorkEntry.Illust(illustration)));
                return true;
            case Mako.Novel novel:
                payload = new ArtworkPayload(new ArtworkPayload.Work(new WorkEntry.NovelWork(novel)));
                return true;
            case BooruPost post:
                payload = new ArtworkPayload(new ArtworkPayload.Booru(post));
                return true;
            case SauceNaoItem item:
                payload = new ArtworkPayload(new ArtworkPayload.SauceNao(item));
                return true;
            default:
                return false;
        }
    }

    public static object? EntryOf(ArtworkPayload? payload) => payload switch
    {
        ArtworkPayload.Work work => Unwrap(work.Entry),
        ArtworkPayload.Booru booru => booru.Post,
        ArtworkPayload.SauceNao sauce => sauce.Item,
        _ => null
    };

    public static bool TryCreateWorkKey(object entry, out string key)
    {
        key = "";
        if (!TryWrap(entry, out var payload))
            return false;
        key = PixevalStorageMethods.ArtworkWorkKey(payload);
        return true;
    }
}
