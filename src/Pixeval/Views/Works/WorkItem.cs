// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Linq;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Pixeval.Views.Works;

public class WorkItem : EntryItem
{
    protected void BlockAuthor_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IWorkEntry { User: var user } work
            || BlockedContentHelper.IsBlockedPlaceholder(work))
            return;

        var blockedContent = this.GetRequiredUiService<IBlockedContentService>();
        var added = blockedContent.TryAddOrUpdateBlockedUser(user);
        if (this.GetVisualAncestors().OfType<WorkView>().FirstOrDefault()?.DataContext is IOperableViewViewModel model)
            model.ReplaceBlockedEntries(blockedContent);
        if (added)
            this.GetRequiredUiService<INotificationService>().ShowSuccess(Strings.BlockedContentAuthorBlockedFormatted(user.Name));
    }

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
