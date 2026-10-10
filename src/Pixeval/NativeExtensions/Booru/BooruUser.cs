// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Native.Booru;

public sealed record BooruUser(string Name, BooruPlatform Platform)
{
    public string Id => Name;

    public string PlatformName => Platform.ToPlatformString();

    public string Description => "";

    public string? AvatarUrl => null;

    public Uri WebsiteUri => new(Platform switch
    {
        BooruPlatform.Danbooru => $"https://danbooru.donmai.us/users?name={Uri.EscapeDataString(Name)}",
        BooruPlatform.Gelbooru => $"https://gelbooru.com/index.php?page=account&s=profile&uname={Uri.EscapeDataString(Name)}",
        BooruPlatform.Sankaku => $"https://chan.sankakucomplex.com/users/{Uri.EscapeDataString(Name)}",
        BooruPlatform.Yandere => $"https://yande.re/user/show?name={Uri.EscapeDataString(Name)}",
        BooruPlatform.Rule34 => $"https://rule34.xxx/index.php?page=account&s=profile&uname={Uri.EscapeDataString(Name)}",
        _ => "about:blank"
    });

    public Uri? AppUri => null;
}
