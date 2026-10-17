// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Mako;

namespace Pixeval.Pixiv;

public interface IBookmarkTag
{
    string? Name { get; }
    long Count { get; }
    bool IsRegistered { get; }
}

public class AddNewBookmarkTag : IBookmarkTag
{
    public string? Name { get; set; } = "";
    public long Count { get; set; } = 0;
    public bool IsRegistered { get; set; } = false;
    public EventHandler<AddNewBookmarkTag, string>? TagAdded;
}

public static class AllBookmarkTag
{
    public static BookmarkTag Instance => new(null!, 0, false);
}

public static class UncategorizedBookmarkTag
{
    public static BookmarkTag Instance => new("未分類", 0, false);
}

public static class BookmarkDetailBookmarkTag
{
    public static BookmarkTag Create(BookmarkTag tag) => tag;
}
