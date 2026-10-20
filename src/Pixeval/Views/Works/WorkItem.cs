// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Pixeval.Views.Works;

public class WorkItem : EntryItem
{
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        Recycle();
        base.OnUnloaded(e);
    }

    internal virtual void Recycle()
    {
        foreach (var descendant in this.GetLogicalDescendants())
            if (descendant is Control { ContextMenu: { } menu })
                menu.Close();

        ClearValue(DataContextProperty);
    }
}
