// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Services.Core;

public sealed class NavigationMenuDocument(string text)
{
    public string Text { get; set; } = text;
}
