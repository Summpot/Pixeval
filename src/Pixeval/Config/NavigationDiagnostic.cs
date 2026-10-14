// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Config;

public partial record struct NavigationDiagnostic
{
    public string PositionText => Line > 0 && Column > 0
        ? $"{Line}:{Column}"
        : "";
}
