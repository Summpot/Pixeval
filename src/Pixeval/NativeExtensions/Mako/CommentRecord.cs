// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Pixeval.Native.Mako;

public partial record CommentRecord
{
    public DateTimeOffset PostedAt =>
        DateTimeOffset.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var postedAt)
            ? postedAt
            : DateTimeOffset.UtcNow;

    [MemberNotNullWhen(true, nameof(StampUrl))]
    public bool IsStamp => Stamp is not null;

    public string? StampUrl => Stamp?.StampUrl;

    public string DisplayText => Comment;
}
