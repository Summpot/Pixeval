// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Config;

namespace Pixeval.Config;

public partial record NavigationParseResult
{
    public NavigationConfiguration? Configuration { get; init; }
}
