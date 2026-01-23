// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.I18N;
using Pixeval.Utilities;

namespace Pixeval.Views.Entry;

public class EntryItem : Button
{
    protected async void OpenInWebBrowser_OnClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: Uri parameter }
            && TopLevel.GetTopLevel(this) is
            {
                Launcher: { } launcher
            })
            _ = await launcher.LaunchUriAsync(parameter);
    }

    protected async void CopyLink_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { CommandParameter: Uri parameter }
            || TopLevel.GetTopLevel(this) is not
            {
                ViewContainer: { } viewContainer,
                Clipboard: { } clipboard
            })
            return;

        await clipboard.SetTextAsync(parameter.OriginalString);

        viewContainer.ShowSuccess(I18NManager.GetResource(EntryItemResources.LinkCopiedToClipboard));
    }
}
