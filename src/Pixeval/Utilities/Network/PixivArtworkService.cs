// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Threading;
using System.Threading.Tasks;
using Misaki;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;

namespace Pixeval.Utilities.Network;

public sealed class PixivArtworkService(MakoClient makoClient) : IGetArtworkService, IPostFavoriteService
{
    public string Platform => IPlatformInfo.Pixiv;

    public async Task<IArtworkInfo> GetArtworkAsync(string id, CancellationToken token = default)
    {
        var rawId = long.Parse(id);
        return await makoClient.GetIllustrationAsync(rawId);
    }

    public async Task<bool> PostFavoriteAsync(string id, bool favorite, CancellationToken token = default)
    {
        var rawId = long.Parse(id);
        var result = favorite
            ? await makoClient.PostBookmarkAsync(false, rawId, "public", null)
            : await makoClient.RemoveBookmarkAsync(false, rawId);
        return result.Success;
    }
}
