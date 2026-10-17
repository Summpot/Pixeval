// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia;
using Pixeval.Booru;
using Pixeval.Mako;

namespace Pixeval.Controls;

/// <summary>
/// Reads layout size from work records so virtualizing panels can place items without realizing containers.
/// </summary>
internal static class ItemNaturalSize
{
    public static bool TryGet(object? item, out Size size)
    {
        switch (item)
        {
            case Illustration { Width: > 0, Height: > 0 } illustration:
                size = new Size(illustration.Width, illustration.Height);
                return true;
            case BooruPost { Width: > 0, Height: > 0 } post:
                size = new Size(post.Width, post.Height);
                return true;
            case WorkEntry entry:
                return TryGet(entry.AsWorkEntry, out size);
            default:
                size = default;
                return false;
        }
    }
}
