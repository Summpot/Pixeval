// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.I18N;
using Pixeval.Native.Mako;

namespace Pixeval.Models.Pixiv;

public record AddNewBookmarkTag() : BookmarkTag("", 0, false)
{
    public EventHandler<AddNewBookmarkTag, string>? TagAdded;
}

public record AllBookmarkTag() : BookmarkTag(null!, 0, false)
{
    public static AllBookmarkTag Instance { get; } = new();

    private static readonly string _TagNameAll = I18NManager.GetResource(MiscResources.TagName.All);

    /// <inheritdoc />
    public override string ToString() => _TagNameAll;
}

public record UncategorizedBookmarkTag() : BookmarkTag("未分類", 0, false)
{
    public static UncategorizedBookmarkTag Instance { get; } = new();

    private static readonly string _TagNameUncategorized = I18NManager.GetResource(MiscResources.TagName.Uncategorized);

    /// <inheritdoc />
    public override string ToString() => _TagNameUncategorized;
}

public record BookmarkDetailBookmarkTag(string TagName, long TagCount, bool IsRegistered) : BookmarkTag(TagName, TagCount, IsRegistered)
{
    public static BookmarkDetailBookmarkTag Create(BookmarkTag tag) => new(tag.Name, tag.Count, tag.IsRegistered);

    /// <inheritdoc />
    public override string ToString() => Name;
}
