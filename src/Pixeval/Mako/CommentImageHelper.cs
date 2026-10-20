// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixeval.Mako;

public static class CommentImageHelper
{
    public static IReadOnlyList<int> StickerIds { get; } =
    [
        .. Enumerable.Range(301, 10),
        .. Enumerable.Range(401, 10),
        .. Enumerable.Range(201, 10),
        .. Enumerable.Range(101, 10)
    ];

    public static IReadOnlyList<int> EmojiIds { get; } = (int[]) Enum.GetValuesAsUnderlyingType<PixivReplyEmoji>();

    public const string StickerDownloadUrl = "https://s.pximg.net/common/images/stamp/generated-stamps/{0}_s.jpg";

    public const string EmojiDownloadUrl = "https://s.pximg.net/common/images/emoji/{0}.png";

    extension(PixivReplyEmoji emoji)
    {
        /// <summary>
        /// Returns the placeholder used to insert the emoji in reply content.
        /// </summary>
        public string PlaceholderKey => $"({emoji.ToString().ToLowerInvariant()})";
    }

}
