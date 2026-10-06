// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;

namespace Pixeval.Native.Filters;

public partial record FilterAnalysisResult
{
    public static FilterAnalysisResult Empty { get; } = new(false, false, [], [], null, null);

    public FilterQuery? Query => QueryHandle;
}
