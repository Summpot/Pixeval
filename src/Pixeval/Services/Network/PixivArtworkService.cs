// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Threading;
using System.Threading.Tasks;
using Pixeval.Pixiv;
using Pixeval.Mako;

namespace Pixeval.Services.Network;

public sealed class PixivArtworkService(MakoClient makoClient)
{
    public string Platform => PlatformConstants.Pixiv;

    public async Task<Illustration> GetArtworkAsync(string id, CancellationToken token = default)
    {
        var rawId = long.Parse(id);
        return await makoClient.GetIllustrationAsync(rawId, token);
    }

    public async Task<bool> PostFavoriteAsync(string id, bool favorite, CancellationToken token = default)
    {
        var rawId = long.Parse(id);
        var result = await makoClient.SetBookmarkAsync(
            SimpleWorkKind.Illustration,
            rawId,
            favorite,
            PrivacyRestrict.Public,
            null, token);
        return result.Success;
    }
}
