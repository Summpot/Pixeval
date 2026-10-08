// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;

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

    public IReadOnlyList<string> RenderMarkdownPages()
    {
        var engine = new Pixeval.Native.Novel.NovelEngine();
        var images = Images
            .Select(x => new Pixeval.Native.Novel.NovelImageRenderDto(x.NovelImageId, x.ThumbnailUrl, ""))
            .ToList();
        var illusts = Illusts
            .Select(x => new Pixeval.Native.Novel.NovelIllustRenderDto(x.Id, x.Page, x.ThumbnailUrl, x.AppUri.OriginalString, x.WebsiteUri.OriginalString, ""))
            .ToList();

        var pages = engine.RenderPagesMarkdown(Text, images, illusts);
        if (pages.Count is 0)
            pages.Add("");

        return pages;
    }
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
