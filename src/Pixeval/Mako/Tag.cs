// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Mako;

public partial record struct Tag
{
    public string Description => ToolTip;

    public string ToolTip => TranslatedName ?? Name;
}
