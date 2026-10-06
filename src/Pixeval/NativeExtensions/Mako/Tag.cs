// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Misaki;

namespace Pixeval.Native.Mako;

public partial record Tag : ITag
{
    public ITagCategory Category => ITagCategory.Empty;

    public string Description => ToolTip;

    string ITranslatedName.TranslatedName => TranslatedName ?? "";

    public string ToolTip => TranslatedName ?? Name;
}
