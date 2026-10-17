// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Pixiv;

namespace Pixeval.Mako;

public partial class MakoClient
{
    public static DateTimeOffset RankingMaxDateTime => DateTimeOffset.UtcNow.AddDays(-1);

    public async Task<List<BookmarkTag>> GetBookmarkTagsAsync(long uid, SimpleWorkKind type, PrivacyRestrict policy, CancellationToken token = default)
    {
        var tags = await WorkBookmarkTagsUnifiedAsync(type, uid, policy, token);
        tags.Insert(0, AllBookmarkTag.Instance);
        tags.Insert(1, UncategorizedBookmarkTag.Instance);
        return tags;
    }

}
