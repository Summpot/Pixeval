// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Native.Config;

public partial record NavigationDiagnostic
{
    public string PositionText => Line > 0 && Column > 0
        ? $"{Line}:{Column}"
        : "";
}
