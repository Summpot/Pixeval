// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;

namespace Pixeval.Mako;

public partial record struct UgoiraMetadata
{
    public IReadOnlyList<int> Delays => [.. Frames.Select(t => t.Delay)];

    public string MediumUrl => ZipUrls.Medium;

    public string LargeUrl => ZipUrls.Medium.Replace("600x600", "1920x1080");

}
