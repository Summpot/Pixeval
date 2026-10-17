// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Localization;
using Pixeval.Pixiv;

namespace Pixeval.Mako;

public partial record struct BookmarkTag : IBookmarkTag
{
    private static string _TagNameAll => Strings.MiscTagNameAll();
    private static string _TagNameUncategorized => Strings.MiscTagNameUncategorized();

    public override string ToString()
    {
        if (Name == null) return _TagNameAll;
        if (Name == "未分類") return _TagNameUncategorized;
        return Name;
    }
}
