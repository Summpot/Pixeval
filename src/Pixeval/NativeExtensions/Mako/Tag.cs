// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Native.Mako;

public partial record Tag
{
    public string Description => ToolTip;

    public string ToolTip => TranslatedName ?? Name;
}
