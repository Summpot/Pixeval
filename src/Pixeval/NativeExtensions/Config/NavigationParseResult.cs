// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Models.Navigation;

namespace Pixeval.Native.Config;

public partial record NavigationParseResult
{
    public NavigationConfiguration? Configuration { get; init; }
}
