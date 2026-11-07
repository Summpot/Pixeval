// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Material3Expressive.Components;
using MaterialSymbols;
using Pixeval.I18N;

namespace Pixeval.Controls;

internal static class MarkdownCodeCopyButton
{
    internal static IconButton Create(Func<string> getText)
    {
        var button = new IconButton
        {
            Symbol = MaterialSymbolCodepoint.ContentCopy,
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("CopyButton");
        var label = I18NManager.GetResource(MiscResources.CopyCode);
        ToolTip.SetTip(button, label);
        AutomationProperties.SetName(button, label);
        button.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(button)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(getText());
        };
        return button;
    }
}
