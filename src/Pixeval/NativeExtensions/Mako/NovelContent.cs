// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;

namespace Pixeval.Native.Mako;

public partial record NovelContent
{
    public static NovelContent CreateDefault() => new(
        0,
        "",
        null,
        null,
        null,
        0,
        "",
        [],
        "",
        "",
        null,
        "",
        null,
        [],
        [],
        null,
        0,
        false,
        "");

    public IReadOnlyList<NovelIllustration> Illustrations => Illusts;

    public DateTimeOffset Date => DateTimeOffset.TryParse(Cdate, out var dt) ? dt : DateTimeOffset.UtcNow;
}

public partial record NovelImage
{
    public string ThumbnailUrl => Urls.X1200;
    public string OriginalUrl => Urls.Original;
}

public partial record NovelIllustration
{
    public string ThumbnailUrl => Illust.Images.Medium;
    public Uri WebsiteUri => new($"https://www.pixiv.net/artworks/{Id}");
    public Uri AppUri => new($"pixeval://illust/{Id}");
}
