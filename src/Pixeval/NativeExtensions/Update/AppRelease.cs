// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Native.Update;

public partial record AppRelease : IComparable<AppRelease>
{
    public Version? ParsedVersion => System.Version.TryParse(Version, out var v) ? v : null;

    public Version VersionObject => ParsedVersion ?? new Version(0, 0, 0, 0);

    public string ReleaseNote => ReleaseNotes;

    public Uri? ReleaseUri => Uri.TryCreate(HtmlUrl, UriKind.Absolute, out var uri) ? uri : null;

    public int CompareTo(AppRelease? other)
    {
        if (ReferenceEquals(this, other))
            return 0;
        if (other is null)
            return 1;

        if (ParsedVersion is { } v1 && other.ParsedVersion is { } v2)
            return v1.CompareTo(v2);

        return string.Compare(Version, other.Version, StringComparison.OrdinalIgnoreCase);
    }
}
