// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Native.Filters;

namespace Pixeval.Filters;

public static class FilterExtensions
{
    public static int End(this FilterTextSpan span) => span.Start + Math.Max(span.Length, 0);

    public static ReadOnlySpan<char> Slice(this FilterTextSpan span, string source)
    {
        var safeStart = Math.Clamp(span.Start, 0, source.Length);
        var safeLength = Math.Clamp(span.Length, 0, source.Length - safeStart);
        return source.AsSpan(safeStart, safeLength);
    }

    public static string GetText(this FilterTextSpan span, string source) => new(span.Slice(source));
}
