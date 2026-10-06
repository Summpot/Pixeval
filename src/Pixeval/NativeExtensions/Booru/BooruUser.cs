// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using Misaki;

namespace Pixeval.Native.Booru;

public sealed record BooruUser(string Name, BooruPlatform Platform) : IUser, IIdEntry
{
    private static readonly Dictionary<string, Uri> s_emptyContact = [];
    private static readonly Dictionary<string, object> s_emptyDict = [];

    public string Id => Name;

    long IIdEntry.Id => long.TryParse(Name, out var id) ? id : 0;

    string IIdentityInfo.Id => Name;

    string IPlatformInfo.Platform => Platform.ToPlatformString();

    public string Description => "";

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

    public IReadOnlyCollection<IImageFrame> Avatar => [];

    public IReadOnlyDictionary<string, Uri> ContactInformation => s_emptyContact;

    public IReadOnlyDictionary<string, object> AdditionalInfo => s_emptyDict;
}
