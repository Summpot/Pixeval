// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.I18N;
using Pixeval.Pixiv;

namespace Pixeval.Mako;

public partial record struct BookmarkTag : IBookmarkTag
{
    private static readonly string _TagNameAll = I18NManager.GetResource(MiscResources.TagName.All);
    private static readonly string _TagNameUncategorized = I18NManager.GetResource(MiscResources.TagName.Uncategorized);

    public override string ToString()
    {
        if (Name == null) return _TagNameAll;
        if (Name == "未分類") return _TagNameUncategorized;
        return Name;
    }
}
