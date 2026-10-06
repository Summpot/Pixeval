// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Misaki;

namespace Pixeval.Native.Booru;

public partial record BooruTag : ITag
{
    public ITagCategory Category => new TagCategory(TagType);

    public string Description => "";

    string ITranslatedName.TranslatedName => "";

    public string ToolTip => Name;
}
